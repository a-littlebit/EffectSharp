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
        private readonly Action<IEnumerable<T>> _batchProcessor; // Synchronous batch processing callback (must run sync)
        private int _intervalMs; // Execution interval in milliseconds (0 = merge tasks rapidly)
        private TaskScheduler _scheduler; // Task scheduler (supports dynamic switching with eventual consistency)
        private readonly ConcurrentQueue<(T Item, long Sequence)> _taskQueue = new ConcurrentQueue<(T Item, long Sequence)>(); // Task queue with sequence numbers for NextTick tracking
        private readonly ConcurrentDictionary<TaskCompletionSource<bool>, long> _waiters
            = new ConcurrentDictionary<TaskCompletionSource<bool>, long>(); // Waiters for FlushAsync and NextTick

        private int _startLoopFlag; // Flag to ensure single batch processing loop
        private CancellationTokenSource _currentDelayCts; // Cancellation source for the current interval delay
        private long _enqueueCounter; // Atomic counter for generating unique task sequence numbers
        private long _processedCounter; // Atomic counter for the highest processed sequence number
        private int _disposed; // Flag to track disposal state
        #endregion

        #region Events
        public event EventHandler<BatchProcessingFailedEventArgs<T>> BatchProcessingFailed;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the TaskBatcher&lt;T&gt; class.
        /// </summary>
        /// <param name="batchProcessor">Synchronous callback to process batches (must execute synchronously)</param>
        /// <param name="intervalMs">Interval between batch executions (0 = no delay, merge tasks)</param>
        /// <param name="scheduler">Initial scheduler to execute batch processing tasks</param>
        /// <exception cref="ArgumentNullException">Thrown if any required parameter is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if interval is negative</exception>
        public TaskBatcher(Action<IEnumerable<T>> batchProcessor, int intervalMs, TaskScheduler scheduler)
        {
            _batchProcessor = batchProcessor ?? throw new ArgumentNullException(nameof(batchProcessor));
            if (intervalMs < 0)
                throw new ArgumentOutOfRangeException(nameof(intervalMs), "Execution interval cannot be negative");
            _intervalMs = intervalMs;
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
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

            // Start the batch processing loop asynchronously (fire-and-forget)
            _ = StartBatchProcessingLoopAsync().ConfigureAwait(false);
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
            long processedSequence = Volatile.Read(ref _processedCounter);

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
        private Task NextTickInternal(long targetSequence, CancellationToken cancellationToken)
        {
            long processedSequence = Volatile.Read(ref _processedCounter);

            // All target tasks are already processed—return completed task
            if (targetSequence <= processedSequence)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.TryAdd(tcs, targetSequence);

            // Register cancellation callback: ALWAYS remove TCS from dictionary to prevent memory leaks
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    // TrySetCanceled will fail if TCS is already completed (SetResult), but we still need to clean up the dictionary
                    tcs.TrySetCanceled(cancellationToken);
                    _waiters.TryRemove(tcs, out _);
                }, useSynchronizationContext: false);
            }

            // Double-check: tasks may have completed while registering the waiter
            processedSequence = Volatile.Read(ref _processedCounter);
            if (targetSequence <= processedSequence)
            {
                tcs.TrySetResult(true);
                _waiters.TryRemove(tcs, out _);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Background loop that handles periodic batch processing.
        /// Runs until the queue is empty or the batcher is disposed.
        /// </summary>
        private async Task StartBatchProcessingLoopAsync()
        {
            // Outermost loop: ensure continuous processing while there are tasks
            while (Volatile.Read(ref _disposed) == 0 && !_taskQueue.IsEmpty)
            {
                if (Interlocked.CompareExchange(ref _startLoopFlag, 1, 0) == 1)
                {
                    // Another loop is already running—exit
                    return;
                }
                // Inner loop: process batches until the queue is empty
                while (Volatile.Read(ref _disposed) == 0 && !_taskQueue.IsEmpty)
                {
                    // Create a new cancellation source for the current interval delay
                    using (var delayCts = new CancellationTokenSource())
                    {
                        Interlocked.Exchange(ref _currentDelayCts, delayCts);
                        try
                        {
                            // Wait for the specified interval (first task waits unless flushed; 0ms = immediate)
                            await Task.Delay(Volatile.Read(ref _intervalMs), delayCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            // Cancellation triggered by FlushAsync—proceed to process immediately
                        }
                        finally
                        {
                            // Clear the reference to the delay cancellation source (thread-safe)
                            Interlocked.Exchange(ref _currentDelayCts, null);
                        }
                    }

                    // Exit loop if disposed during the delay
                    if (Volatile.Read(ref _disposed) == 1) break;

                    // Process one batch of tasks (uses the latest scheduler)
                    await ProcessBatchAsync().ConfigureAwait(false);
                }
                Interlocked.Exchange(ref _startLoopFlag, 0); // Reset loop flag
            }
        }

        /// <summary>
        /// Processes a single batch of tasks from the queue.
        /// </summary>
        private async Task ProcessBatchAsync()
        {
            // Optimize: Skip List creation if queue is empty (ConcurrentQueue.IsEmpty is snapshot, but reduces empty List overhead)
            if (_taskQueue.IsEmpty)
            {
                return;
            }

            // Dequeue all tasks currently in the queue (atomic operation)
            var batch = new List<(T Item, long Sequence)>(_taskQueue.Count);
            long maxProcessedSeq = 0;
            while (_taskQueue.TryDequeue(out var taskWithSeq))
            {
                batch.Add(taskWithSeq);
                if (taskWithSeq.Sequence > maxProcessedSeq)
                {
                    maxProcessedSeq = taskWithSeq.Sequence;
                }
            }

            // Exit if no tasks to process (queue emptied between IsEmpty check and Dequeue)
            if (batch.Count == 0)
            {
                return;
            }

            // Extract task data and get the latest scheduler
            var taskData = batch.Select(t => t.Item);
            var currentScheduler = Scheduler;

            // Run the synchronous batch processor on the specified scheduler
            var processTask = Task.Factory.StartNew(
                () =>
                {
                    _batchProcessor(taskData); // synchronous callback
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                currentScheduler);

            Exception ex = null;
            // Wait and handle exceptions: propagate failure to caller
            try
            {
                await processTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation (expected flow)
                throw;
            }
            catch (Exception e)
            {
                ex = e;
            }

            // Update processed counter
            long observedProcessedSeq;
            do
            {
                observedProcessedSeq = Volatile.Read(ref _processedCounter);
                if (maxProcessedSeq <= observedProcessedSeq)
                {
                    break; // No update needed
                }
            } while (Interlocked.CompareExchange(ref _processedCounter, maxProcessedSeq, observedProcessedSeq) != observedProcessedSeq);

            // Notify waiters
            if (ex == null)
            {
                NotifyNextTickWaiters();
            }
            else
            {
                NotifyNextTickWaitersForException(ex);
                BatchProcessingFailed?.Invoke(this, new BatchProcessingFailedEventArgs<T>(ex, taskData.ToList()));
            }
        }

        /// <summary>
        /// Notifies NextTick waiters that their target sequence number has been processed.
        /// </summary>
        private void NotifyNextTickWaiters()
        {
            if (_waiters.IsEmpty)
            {
                return;
            }

            long processedSeq = Volatile.Read(ref _processedCounter);

            // Process waiters in queue order (guaranteed to be in sequence order)
            foreach (var pair in _waiters.ToList())
            {
                var tcs = pair.Key;
                var targetSeq = pair.Value;
                if (targetSeq <= processedSeq)
                {
                    // Target sequence has been processed—complete the waiter
                    tcs.TrySetResult(true);
                    _waiters.TryRemove(tcs, out _);
                }
            }
        }

        /// <summary>
        /// Notifies all pending waiters for the next tick that an exception has occurred, causing their associated
        /// tasks to complete with the specified exception.
        /// </summary>
        /// <param name="ex">The exception to propagate to all waiters whose target sequence has been processed. Cannot be null.</param>
        private void NotifyNextTickWaitersForException(Exception ex)
        {
            if (_waiters.IsEmpty)
            {
                return;
            }

            long processedSeq = Volatile.Read(ref _processedCounter);

            foreach (var pair in _waiters.ToList())
            {
                var tcs = pair.Key;
                var targetSeq = pair.Value;
                if (targetSeq <= processedSeq)
                {
                    tcs.TrySetException(ex);
                    _waiters.TryRemove(tcs, out _);
                }
            }
        }

        /// <summary>
        /// Cancels the current interval delay (if active).
        /// Used by FlushAsync to trigger immediate processing.
        /// </summary>
        private void CancelCurrentDelay()
        {
            // Safely retrieve and clear the current delay cancellation source
            var delayCts = Interlocked.Exchange(ref _currentDelayCts, null);

            // Cancel the delay if it's active (avoid redundant calls)
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
                // Cancel all pending delays and clean up
                var delayCts = Interlocked.Exchange(ref _currentDelayCts, null);
                if (delayCts != null)
                {
                    delayCts.Cancel();
                    delayCts.Dispose();
                }

                // Cancel all NextTick waiters and clean up dictionary (prevent memory leaks)
                foreach (var pair in _waiters)
                {
                    var tcs = pair.Key;
                    tcs.TrySetCanceled();
                    _waiters.TryRemove(tcs, out _);
                }
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