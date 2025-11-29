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
        private readonly ConcurrentQueue<T> _taskQueue = new ConcurrentQueue<T>(); // Task queue

        private int _startLoopFlag; // Flag to ensure single batch processing loop
        private CancellationTokenSource _currentDelayCts; // Cancellation source for the current interval delay
        private TaskCompletionSource<bool> _nextTickTcs
            = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); // TCS for NextTick
        private readonly object _nextTickLock = new object(); // Lock for NextTick TCS
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

            _taskQueue.Enqueue(item);

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

            // Cancel periodic delay to trigger immediate processing of pending tasks
            CancelCurrentDelay();

            // Wait for all tasks enqueued at the time of calling to complete
            await NextTick(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously waits for all tasks enqueued at the time of calling to complete.
        /// Ignores tasks enqueued after this method is called.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to abort the wait</param>
        /// <returns>Task representing the completion of all target tasks</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the batcher is disposed</exception>
        /// <exception cref="OperationCanceledException">Thrown if the wait is canceled</exception>
        public async Task NextTick(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            Task nextTickTask;
            lock (_nextTickLock)
            {
                nextTickTask = _nextTickTcs.Task;
                if (_taskQueue.IsEmpty)
                {
                    // No pending tasks; complete immediately
                    return;
                }
            }
            await WithCancellationAsync(nextTickTask, cancellationToken).ConfigureAwait(false);
        }
        #endregion

        #region Core Logic (Internal)
        /// <summary>
        /// Wrapper to add cancellation support to a Task that does not natively support it.
        /// </summary>
        /// <param name="task">Task to wrap. </param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Completed task or throws if cancelled. </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is canceled</exception>
        private static async Task WithCancellationAsync(Task task, CancellationToken cancellationToken)
        {
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

            // Dequeue all tasks currently in the queue
            var batch = new List<T>(_taskQueue.Count);
            while (_taskQueue.TryDequeue(out var taskItem))
            {
                batch.Add(taskItem);
            }

            TaskCompletionSource<bool> oldTcs;
            lock (_nextTickLock)
            {
                // Create a new TCS for the next tick waiters
                oldTcs = _nextTickTcs;
                _nextTickTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                // Ensure all tasks enqueued before this batch are completed when the old TCS is set
                while (_taskQueue.TryDequeue(out var taskItem))
                {
                    batch.Add(taskItem);
                }
                // Here we may have dequeued more items; however, when next tick caller gets the lock,
                // either the queue will be empty or the new TCS will cover them.
            }

            var currentScheduler = Scheduler;

            // Run the synchronous batch processor on the specified scheduler
            var processTask = Task.Factory.StartNew(
                () =>
                {
                    _batchProcessor(batch); // synchronous callback
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                currentScheduler);

            // Wait and handle exceptions: propagate failure to caller
            try
            {
                await processTask.ConfigureAwait(false);
                // Signal completion to all waiters for this batch
                oldTcs.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                // Signal cancellation to all waiters for this batch
                oldTcs.TrySetCanceled();
            }
            catch (Exception e)
            {
                // Signal failure to all waiters for this batch
                oldTcs.TrySetException(e);
                // Raise the BatchProcessingFailed event
                BatchProcessingFailed?.Invoke(this, new BatchProcessingFailedEventArgs<T>(e, batch));
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

                lock (_nextTickLock)
                {
                    // Set the current TCS to canceled to unblock any waiters
                    _nextTickTcs.TrySetCanceled();
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
