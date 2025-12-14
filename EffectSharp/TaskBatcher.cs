using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Batch task processor that supports periodic execution, asynchronous immediate flushing,
    /// asynchronous batch completion waiting, and dynamic task scheduler switching.
    /// </summary>
    /// <typeparam name="T">Type of data structure for tasks to process</typeparam>
    public class TaskBatcher<T> : IDisposable
    {
        #region Private Fields
        private readonly Func<List<T>, Task> _batchProcessor; // Synchronous batch processing callback (must run sync)
        private int _intervalMs; // Execution interval in milliseconds (0 = merge tasks rapidly)
        private TaskScheduler _scheduler; // Task scheduler (supports dynamic switching with eventual consistency)
        private readonly SemaphoreSlim _consumerSemaphore;
        private readonly ConcurrentQueue<(T Item, long Sequence)> _taskQueue = new ConcurrentQueue<(T Item, long Sequence)>(); // Task queue with sequence numbers for NextTick tracking

        private int _startLoopFlag; // Flag to ensure single batch processing loop
        private CancellationTokenSource _currentDelayCts; // Cancellation source for the current interval delay
        private long _enqueueCounter; // Atomic counter for generating unique task sequence numbers
        private long _dequeuedCounter; // Atomic counter for the highest dequeued sequence number
        private TickState _tickState = new TickState(); // State for NextTick tracking
        private int _disposed; // Flag to track disposal state
        #endregion

        #region Internal Types
        private class TickState
        {
            internal readonly long ProcessedSequence;
            internal readonly TaskCompletionSource<bool> NextTickTcs;
            internal TickState(long processedSequence = 0)
            {
                ProcessedSequence = processedSequence;
                NextTickTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Raised when processing a batch fails with an exception.
        /// </summary>
        public event EventHandler<BatchProcessingFailedEventArgs<T>> BatchProcessingFailed;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="TaskBatcher{T}" /> class.
        /// </summary>
        /// <param name="batchProcessor">Asynchronous callback to process batches</param>
        /// <param name="intervalMs">Interval between batch executions (0 = no delay, merge tasks)</param>
        /// <param name="scheduler">Initial scheduler to execute batch processing tasks</param>
        /// <param name="maxConsumers">Maximum number of concurrent batch processing tasks</param>
        /// <exception cref="ArgumentNullException">Thrown if any required parameter is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if interval is negative</exception>
        public TaskBatcher(Func<List<T>, Task> batchProcessor, int intervalMs, TaskScheduler scheduler, int maxConsumers = 1)
        {
            _batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
            if (intervalMs < 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), "Execution interval cannot be negative");
            _intervalMs = intervalMs;
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _consumerSemaphore = new SemaphoreSlim(maxConsumers, maxConsumers);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskBatcher{T}" /> class with a synchronous batch processor.
        /// </summary>
        /// <param name="batchProcessor">Synchronous callback to process batches</param>
        /// <param name="intervalMs">Interval between batch executions (0 = no delay, merge tasks)</param>
        /// <param name="scheduler">Initial scheduler to execute batch processing tasks</param>
        /// <param name="maxConsumers">Maximum number of concurrent batch processing tasks</param>
        /// <exception cref="ArgumentNullException">Thrown if any required parameter is null</exception>
        public TaskBatcher(Action<List<T>> batchProcessor, int intervalMs, TaskScheduler scheduler, int maxConsumers = 1)
            : this(items => {
                batchProcessor.Invoke(items);
                return Task.CompletedTask;
            }, intervalMs, scheduler, maxConsumers)
        {
            if (batchProcessor == null)
                throw new ArgumentNullException(nameof(batchProcessor));
        }
        #endregion

        #region Public Properties
        /// <summary>
        /// Interval between batch executions in milliseconds (0 = no delay, merge tasks rapidly).
        /// Supports dynamic updating with eventual consistency:
        /// - Current in-progress delays use the old interval
        /// - Subsequent delays use the new interval
        /// </summary>
        public int IntervalMs
        {
            get => Volatile.Read(ref _intervalMs); // Ensure latest value is read across threads
            set
            {
                ThrowIfDisposed();
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), "Execution interval cannot be negative");
                Interlocked.Exchange(ref _intervalMs, value);
            }
        }

        /// <summary>
        /// Task scheduler for batch execution. Supports dynamic switching with eventual consistency:
        /// - Current in-progress batches use the old scheduler
        /// - Subsequent batches use the new scheduler
        /// </summary>
        public TaskScheduler Scheduler
        {
            get => Volatile.Read(ref _scheduler); // Ensure latest value is read across threads
            set
            {
                ThrowIfDisposed();
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "Scheduler cannot be null");

                // Avoid unnecessary atomic operations if setting the same scheduler
                var currentScheduler = Volatile.Read(ref _scheduler);
                if (currentScheduler != value)
                {
                    Interlocked.Exchange(ref _scheduler, value);
                }
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Enqueues a single task for batch processing.
        /// </summary>
        /// <param name="item">Task data to enqueue</param>
        /// <exception cref="ObjectDisposedException">Thrown if the batcher is disposed</exception>
        public void Enqueue(T item)
        {
            ThrowIfDisposed();

            long sequence = Interlocked.Increment(ref _enqueueCounter);
            _taskQueue.Enqueue((item, sequence));

            // Ensure cancellation source exists for the current task
            EnsureDelayCancellationSource();

            if (Volatile.Read(ref _startLoopFlag) == 0)
            {
                // Start the batch processing loop asynchronously (fire-and-forget)
                _ = RunProcessingLoopAsync();
            }
        }

        /// <summary>
        /// Asynchronously flushes all tasks enqueued BEFORE this method is called.
        /// Triggers immediate processing (ignores interval) and waits for all pre-enqueued tasks to complete.
        /// Tasks enqueued AFTER this method is called are ignored.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to abort the flush operation</param>
        /// <returns>Task representing the completion of all pre-enqueued tasks</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the batcher is disposed</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled</exception>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            // Step 1: Take a snapshot of the maximum sequence number enqueued BEFORE this flush call
            long targetSequence = Volatile.Read(ref _enqueueCounter);
            var tickState = Volatile.Read(ref _tickState);
            long processedSequence = tickState.ProcessedSequence;

            // If all pre-enqueued tasks are already processed, return immediately
            if (targetSequence <= processedSequence)
            {
                return;
            }

            // Step 2: Cancel periodic delay to trigger immediate processing of pending tasks
            CancelCurrentDelay();

            // Step 3: Wait for ALL tasks with sequence ≤ targetSequence to complete (reuse NextTick logic)
            await NextTickInternal(targetSequence, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously waits for all tasks enqueued at the time of calling to complete.
        /// Ignores tasks enqueued after this method is called.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to abort the wait</param>
        /// <returns>Task representing the completion of all target tasks</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the batcher is disposed</exception>
        /// <exception cref="OperationCanceledException">Thrown if the wait is canceled</exception>
        public Task NextTick(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            // Wait for all tasks enqueued BEFORE this method call
            long targetSequence = Volatile.Read(ref _enqueueCounter);
            return NextTickInternal(targetSequence, cancellationToken);
        }
        #endregion

        #region Core Logic (Internal)
        /// <summary>
        /// Internal implementation to wait for tasks with sequence ≤ targetSequence.
        /// Reused by both NextTick (public) and FlushAsync (to ensure consistency).
        /// </summary>
        /// <param name="targetSequence">Maximum sequence number to wait for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        private async Task NextTickInternal(long targetSequence, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tickState = Volatile.Read(ref _tickState);

            while (targetSequence > tickState.ProcessedSequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WithCancellationAsync(tickState.NextTickTcs.Task, cancellationToken).ConfigureAwait(false);
                tickState = Volatile.Read(ref _tickState);
            }
        }

        /// <summary>
        /// Wrapper to add cancellation support to a Task that does not natively support it.
        /// </summary>
        /// <param name="task">Task to wrap. </param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Completed task or throws if cancelled. </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled</exception>
        private static async Task WithCancellationAsync(Task task, CancellationToken cancellationToken)
        {
            if (task.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                await task.ConfigureAwait(false);
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Create a "cancellation signal task" that completes when cancellation is requested
            var cancellationCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Register cancellation callback to complete the cancellation signal task
            using (cancellationToken.Register(() => cancellationCompletionSource.TrySetResult(true)))
            {
                // Wait for either the original task or the cancellation signal task to complete
                var completedTask = await Task.WhenAny(task, cancellationCompletionSource.Task).ConfigureAwait(false);

                // If the cancellation signal task completed first, throw OperationCanceledException
                if (completedTask == cancellationCompletionSource.Task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                // Wait for the original task to complete to propagate exceptions if any
                await task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Background loop that handles periodic batch processing.
        /// Runs until the queue is empty or the batcher is disposed.
        /// </summary>
        private async Task RunProcessingLoopAsync()
        {
            // Track time cost of last batch processing and subcontract the next delay accordingly
            var lastTimeCostMs = 0;
            // Outermost loop: ensure continuous processing while there are tasks
            while (Volatile.Read(ref _disposed) == 0 && !_taskQueue.IsEmpty)
            {
                if (Interlocked.CompareExchange(ref _startLoopFlag, 1, 0) != 0)
                {
                    // Another loop is already running—exit
                    return;
                }
                try
                {
                    // Inner loop: process batches until the queue is empty
                    while (Volatile.Read(ref _disposed) == 0 && !_taskQueue.IsEmpty)
                    {
                        // Create a new cancellation source for the current interval delay
                        var delayCts = EnsureDelayCancellationSource();
                        try
                        {
                            // Check for cancellation before starting the delay
                            delayCts.Token.ThrowIfCancellationRequested();
                            // Wait for the specified interval (first task waits unless flushed; 0ms = immediate)
                            var intervalMs = Volatile.Read(ref _intervalMs);
                            var delayMs = intervalMs - lastTimeCostMs;
                            if (delayMs > 0)
                                await Task.Delay(delayMs, delayCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Cancellation triggered by FlushAsync—proceed to process immediately
                        }
                        finally
                        {
                            // Clear the reference to the delay cancellation source
                            Interlocked.Exchange(ref _currentDelayCts, null);
                            delayCts.Dispose();
                        }

                        // Exit loop if disposed during the delay
                        if (Volatile.Read(ref _disposed) == 1) break;

                        // Measure time cost of batch processing for next delay calculation
                        var startTime = Environment.TickCount;
                        await StartBatchProcessingAsync().ConfigureAwait(false);
                        var endTime = Environment.TickCount;
                        lastTimeCostMs = endTime - startTime;
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _startLoopFlag, 0); // Reset loop flag
                }
            }
        }

        public async Task StartBatchProcessingAsync()
        {
            // Acquire semaphore to limit concurrent batch processing
            await _consumerSemaphore.WaitAsync().ConfigureAwait(false);

            // Capture the current scheduler for this batch
            var scheduler = Volatile.Read(ref _scheduler);
            // Create a TaskCompletionSource to signal when dequeuing is done
            var dequeuedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Schedule batch processing on the specified scheduler
            _ = Task.Factory.StartNew(async () =>
            {
                await ProcessBatchAsync(dequeuedTcs).ConfigureAwait(false);
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.RunContinuationsAsynchronously,
            scheduler).Unwrap();

            // Wait for the batch to be dequeued before next batch dequeuing
            await dequeuedTcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Processes a single batch of tasks from the queue.
        /// </summary>
        private async Task ProcessBatchAsync(TaskCompletionSource<bool> dequeuedTcs)
        {
            if (_taskQueue.IsEmpty)
            {
                dequeuedTcs.TrySetResult(true);
                _consumerSemaphore.Release();
                return;
            }

            // Dequeue all tasks currently in the queue and ensure sequence continuity
            var batch = new List<T>(_taskQueue.Count);
            var dequeuedSeq = _dequeuedCounter;
            var expectedSeq = new HashSet<long>();
            bool dequeuedAny;
            while ((dequeuedAny = _taskQueue.TryDequeue(out var taskWithSeq)) || expectedSeq.Count != 0)
            {
                if (!dequeuedAny)
                {
                    while (!_taskQueue.TryDequeue(out taskWithSeq))
                    {
                        // Wait for missing tasks to arrive
                        await Task.Yield();
                    }
                }
                var (task, seq) = taskWithSeq;
                batch.Add(task);
                if (seq < dequeuedSeq)
                {
                    expectedSeq.Remove(seq);
                }
                else
                {
                    if (seq != dequeuedSeq + 1)
                    {
                        for (long missingSeq = dequeuedSeq + 1; missingSeq < seq; missingSeq++)
                        {
                            expectedSeq.Add(missingSeq);
                        }
                    }
                    dequeuedSeq = seq;
                }
            }
            var lastDequeuedSeq = _dequeuedCounter;
            _dequeuedCounter = dequeuedSeq;
            dequeuedTcs.TrySetResult(true);

            // Exit if no tasks to process (queue emptied between IsEmpty check and Dequeue)
            if (batch.Count == 0)
            {
                _consumerSemaphore.Release();
                return;
            }

            // Run the synchronous batch processor on the specified scheduler
            try
            {
                await _batchProcessor(batch).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await AfterBatchProcess(lastDequeuedSeq, dequeuedSeq, batch, ex).ConfigureAwait(false);
                return;
            }
            await AfterBatchProcess(lastDequeuedSeq, dequeuedSeq, batch).ConfigureAwait(false);
        }

        private async Task AfterBatchProcess(long beforeProcessSeq, long processedSeq, List<T> batch = null, Exception ex = null)
        {
            // Release the semaphore to allow other batch processing tasks
            _consumerSemaphore.Release();

            // Ensure all prior batches are fully processed before updating the processed counter
            var oldTickState = Volatile.Read(ref _tickState);
            while (beforeProcessSeq != oldTickState.ProcessedSequence)
            {
                try
                {
                    await NextTickInternal(beforeProcessSeq, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore exceptions here; they may be handled in other NextTick calls
                }
                oldTickState = Volatile.Read(ref _tickState);
            }

            var newTickState = new TickState(processedSeq);
            oldTickState = Interlocked.Exchange(ref _tickState, newTickState);
            if (ex != null)
            {
                oldTickState.NextTickTcs.TrySetException(ex);
                BatchProcessingFailed?.Invoke(this, new BatchProcessingFailedEventArgs<T>(ex, batch));
            }
            else
            {
                oldTickState.NextTickTcs.TrySetResult(true);
            }
        }

        private CancellationTokenSource EnsureDelayCancellationSource()
        {
            var existingDelayCts = Volatile.Read(ref _currentDelayCts);
            if (existingDelayCts != null)
                return existingDelayCts;

            // Create a new cancellation source for the current interval delay
            var newDelayTcs = new CancellationTokenSource();
            // Attempt to set it
            var oldDelayTcs = Interlocked.CompareExchange(ref _currentDelayCts, newDelayTcs, null);
            if (oldDelayTcs != null)
            {
                // Another thread already set it—dispose the new one and return the existing
                newDelayTcs.Dispose();
                return oldDelayTcs;
            }
            return newDelayTcs;
        }

        /// <summary>
        /// Cancels the current interval delay (if active).
        /// </summary>
        private void CancelCurrentDelay()
        {
            var delayCts = Volatile.Read(ref _currentDelayCts);

            if (delayCts != null && !delayCts.IsCancellationRequested)
            {
                delayCts.Cancel();
            }
        }
        #endregion

        #region Disposal
        /// <summary>
        /// Releases all resources used by the TaskBatcher&lt;T&gt;.
        /// Does not wait for in-progress batches to complete (call FlushAsync first if needed).
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True to release managed resources; false for unmanaged only</param>
        protected virtual void Dispose(bool disposing)
        {
            var disposed = Interlocked.Exchange(ref _disposed, 1);

            if (disposed == 0 && disposing)
            {
                // Speed cleanup
                CancelCurrentDelay();

                // Dispose the semaphore
                _consumerSemaphore.Dispose();
            }
        }

        /// <summary>
        /// Throws an exception if the batcher has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if disposed</exception>
        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) == 1)
                throw new ObjectDisposedException(nameof(TaskBatcher<T>));
        }
        #endregion
    }

    public class BatchProcessingFailedEventArgs<T> : EventArgs
    {
        public Exception Exception { get; }
        public List<T> FailedItems { get; }
        public BatchProcessingFailedEventArgs(Exception exception, List<T> failedItems)
        {
            Exception = exception;
            FailedItems = failedItems;
        }
    }
}