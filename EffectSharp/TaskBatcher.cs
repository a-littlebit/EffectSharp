using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Batch task processor that supports periodic execution, asynchronous immediate flushing, 
/// asynchronous batch completion waiting, and dynamic task scheduler switching.
/// </summary>
/// <typeparam name="T">Type of data structure for tasks to process</typeparam>
public class TaskBatcher<T> : IDisposable
{
    #region Private Fields
    private readonly Action<IEnumerable<T>> _batchProcessor; // Synchronous batch processing callback (must run sync)
    private readonly int _intervalMs; // Execution interval in milliseconds (0 = merge tasks rapidly)
    private TaskScheduler _scheduler; // Task scheduler (supports dynamic switching with eventual consistency)
    private readonly ConcurrentQueue<(T Item, long Sequence)> _taskQueue = new ConcurrentQueue<(T Item, long Sequence)>(); // Task queue with sequence numbers for NextTick tracking
    private readonly object _stateLock = new object(); // Synchronization lock for critical state changes
    private readonly ConcurrentQueue<TaskCompletionSource<bool>> _nextTickTcsQueue = new ConcurrentQueue<TaskCompletionSource<bool>>(); // Queue for NextTick waiters
    private readonly ConcurrentDictionary<TaskCompletionSource<bool>, long> _nextTickTargets = new ConcurrentDictionary<TaskCompletionSource<bool>, long>(); // Maps waiters to their target sequence number

    private bool _isProcessing; // Flag to prevent parallel batch processing
    private Task _batchLoopTask; // Background loop task for periodic processing
    private CancellationTokenSource _currentDelayCts; // Cancellation source for the current interval delay
    private long _enqueueCounter; // Atomic counter for generating unique task sequence numbers
    private long _processedCounter; // Atomic counter for the highest processed sequence number
    private bool _disposed; // Flag to track disposal state
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

        // Initialize loop task to completed state to avoid null reference exceptions
        _batchLoopTask = Task.CompletedTask;
    }
    #endregion

    #region Public Properties
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

        // Generate a unique sequence number (atomic increment to ensure ordering)
        long sequence = Interlocked.Increment(ref _enqueueCounter);
        _taskQueue.Enqueue((item, sequence));

        // Start the processing loop if it's not running (lock guarantees memory visibility, no need for Volatile.Read)
        lock (_stateLock)
        {
            if (_batchLoopTask.IsCompleted)
            {
                _batchLoopTask = StartBatchProcessingLoopAsync();
                // trace if exception occurs during loop
                _batchLoopTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        System.Diagnostics.Trace.TraceError($"TaskBatcher processing loop failed: {t.Exception}");
                    }
                },
                TaskContinuationOptions.OnlyOnFaulted);
            }
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

        // Create TCS (no RunContinuationsAsynchronously for .NET Standard 2.0 compatibility)
        var tcs = new TaskCompletionSource<bool>();
        _nextTickTargets.TryAdd(tcs, targetSequence);
        _nextTickTcsQueue.Enqueue(tcs);

        // Register cancellation callback: ALWAYS remove TCS from dictionary to prevent memory leaks
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                // TrySetCanceled will fail if TCS is already completed (SetResult), but we still need to clean up the dictionary
                tcs.TrySetCanceled(cancellationToken);
                _nextTickTargets.TryRemove(tcs, out _);
            }, useSynchronizationContext: false);
        }

        // Double-check: tasks may have completed while registering the waiter
        processedSequence = Volatile.Read(ref _processedCounter);
        if (targetSequence <= processedSequence)
        {
            if (tcs.TrySetResult(true))
            {
                _nextTickTargets.TryRemove(tcs, out _);
            }
            else
            {
                // In rare cases (TCS already canceled), clean up dictionary
                _nextTickTargets.TryRemove(tcs, out _);
            }
        }

        return tcs.Task;
    }

    /// <summary>
    /// Background loop that handles periodic batch processing.
    /// Runs until the queue is empty or the batcher is disposed.
    /// </summary>
    private async Task StartBatchProcessingLoopAsync()
    {
        while (!_disposed && !_taskQueue.IsEmpty)
        {
            // Create a new cancellation source for the current interval delay
            using (var delayCts = new CancellationTokenSource())
            {
                // Safely assign the current delay cancellation source
                lock (_stateLock)
                {
                    if (_disposed) break;
                    _currentDelayCts = delayCts;
                }

                try
                {
                    // Wait for the specified interval (first task waits unless flushed; 0ms = immediate)
                    await Task.Delay(_intervalMs, delayCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!_disposed)
                {
                    // Cancellation triggered by FlushAsync—proceed to process immediately
                }
                finally
                {
                    // Clear the reference to the delay cancellation source (thread-safe)
                    lock (_stateLock)
                    {
                        if (_currentDelayCts == delayCts)
                        {
                            _currentDelayCts = null;
                        }
                    }
                }
            }

            // Exit loop if disposed during the delay
            if (_disposed) break;

            // Process one batch of tasks (uses the latest scheduler)
            await ProcessBatchAsync().ConfigureAwait(false);
        }

        // Reset the loop task to completed state when done (prevents memory leaks)
        lock (_stateLock)
        {
            if (_batchLoopTask.IsCompleted)
            {
                _batchLoopTask = Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Processes a single batch of tasks from the queue.
    /// Ensures only one batch runs at a time.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to abort processing</param>
    private async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        // Acquire lock to check/set processing state (prevent parallel batches)
        lock (_stateLock)
        {
            if (_disposed || _isProcessing)
            {
                return;
            }
            _isProcessing = true;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Optimize: Skip List creation if queue is empty (ConcurrentQueue.IsEmpty is snapshot, but reduces empty List overhead)
            if (_taskQueue.IsEmpty)
            {
                return;
            }

            // Dequeue all tasks currently in the queue (atomic operation)
            var batch = new List<(T Item, long Sequence)>();
            while (_taskQueue.TryDequeue(out var taskWithSeq))
            {
                batch.Add(taskWithSeq);
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
                    cancellationToken.ThrowIfCancellationRequested();
                    _batchProcessor(taskData); // synchronous callback
                },
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                currentScheduler);

            // Wait and handle exceptions: propagate failure to caller
            try
            {
                await WaitTaskAsync(processTask, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Re-throw cancellation (expected flow)
                throw;
            }
            catch (Exception ex)
            {
                // Log with batch details, then re-throw to notify caller
                System.Diagnostics.Trace.TraceError(
                    $"TaskBatcher batch processing failed (batch size: {batch.Count}, max sequence: {batch.Max(t => t.Sequence)}): {ex}");
                throw new InvalidOperationException("Failed to process batch", ex);
            }

            // Update processed counter with CAS (monotonic increment guarantee)
            long maxProcessedSeq = batch.Max(t => t.Sequence);
            long observed;
            int spinCount = 0;
            do
            {
                if (spinCount++ > 10)
                {
                    System.Diagnostics.Trace.TraceWarning(
                        $"TaskBatcher CAS loop timeout: maxProcessedSeq={maxProcessedSeq}, current={Volatile.Read(ref _processedCounter)}");
                    break;
                }
                observed = Volatile.Read(ref _processedCounter);
                if (maxProcessedSeq <= observed) break;
            } while (Interlocked.CompareExchange(ref _processedCounter, maxProcessedSeq, observed) != observed);

            // Notify waiters (only if batch processed successfully)
            NotifyNextTickWaiters();
        }
        finally
        {
            // Reset processing state (release lock for next batch)
            lock (_stateLock)
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>
    /// Notifies NextTick waiters that their target sequence number has been processed.
    /// Processes waiters in order (sequence numbers are strictly increasing).
    /// </summary>
    private void NotifyNextTickWaiters()
    {
        if (_nextTickTcsQueue.IsEmpty)
        {
            return;
        }

        long processedSeq = Volatile.Read(ref _processedCounter);
        var completedWaiters = new List<TaskCompletionSource<bool>>();

        // Process waiters in queue order (guaranteed to be in sequence order)
        while (_nextTickTcsQueue.TryDequeue(out var tcs))
        {
            if (_nextTickTargets.TryGetValue(tcs, out long targetSeq))
            {
                // If target sequence is processed, complete the waiter
                if (targetSeq <= processedSeq)
                {
                    tcs.TrySetResult(true);
                    completedWaiters.Add(tcs);
                }
                else
                {
                    // Queue is ordered—remaining waiters have higher sequences, re-enqueue and exit
                    _nextTickTcsQueue.Enqueue(tcs);
                    break;
                }
            }
        }

        // Clean up completed waiters from the dictionary (prevent memory leaks)
        foreach (var tcs in completedWaiters)
        {
            _nextTickTargets.TryRemove(tcs, out _);
        }
    }

    /// <summary>
    /// Cancels the current interval delay (if active).
    /// Used by FlushAsync to trigger immediate processing.
    /// </summary>
    private void CancelCurrentDelay()
    {
        CancellationTokenSource delayCts = null;

        // Safely retrieve and clear the current delay cancellation source
        lock (_stateLock)
        {
            delayCts = _currentDelayCts;
            _currentDelayCts = null;
        }

        // Cancel the delay if it's active (avoid redundant calls)
        if (delayCts != null && !delayCts.IsCancellationRequested)
        {
            delayCts.Cancel();
        }
    }

    /// <summary>
    /// .NET Standard 2.0-compatible replacement for Task.WaitAsync (available in .NET 5+).
    /// Waits for a task to complete or a cancellation token to trigger, propagating exceptions.
    /// </summary>
    /// <param name="task">Task to wait for</param>
    /// <param name="cancellationToken">Cancellation token to abort the wait</param>
    /// <returns>Task representing the completion of the target task</returns>
    /// <exception cref="OperationCanceledException">Thrown if the wait is canceled</exception>
    private async Task WaitTaskAsync(Task task, CancellationToken cancellationToken)
    {
        // Return immediately if the task is already completed
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        // Simulate WaitAsync with Task.WhenAny (TaskCompletionSource doesn't implement IDisposable—no using needed)
        var cancellationTcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => cancellationTcs.TrySetCanceled(cancellationToken)))
        {
            var completedTask = await Task.WhenAny(task, cancellationTcs.Task).ConfigureAwait(false);

            // Throw if cancellation was requested
            if (completedTask == cancellationTcs.Task)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            // Await the original task to propagate exceptions (e.g., task faulted)
            await task.ConfigureAwait(false);
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
        if (_disposed)
            return;

        if (disposing)
        {
            // Cancel all pending delays and clean up
            lock (_stateLock)
            {
                if (_currentDelayCts != null)
                {
                    _currentDelayCts.Cancel();
                    _currentDelayCts.Dispose();
                    _currentDelayCts = null;
                }
                _disposed = true;
            }

            // Cancel all NextTick waiters and clean up dictionary (prevent memory leaks)
            while (_nextTickTcsQueue.TryDequeue(out var tcs))
            {
                tcs.TrySetCanceled();
                _nextTickTargets.TryRemove(tcs, out _);
            }

            // Clear remaining entries in _nextTickTargets (defensive cleanup)
            foreach (var tcs in _nextTickTargets.Keys.ToList())
            {
                tcs.TrySetCanceled();
                _nextTickTargets.TryRemove(tcs, out _);
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Throws an exception if the batcher has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if disposed</exception>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TaskBatcher<T>));
    }
    #endregion
}