using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp;

/// <summary>
/// A generic task batcher that queues multiple task items and
/// executes a processing function to handle all queued tasks at once after a specified delay.
/// This helps to reduce the performance overhead caused by frequent operations, such as merging multiple UI updates.
/// </summary>
/// <typeparam name="T">Data type processed by the task. </typeparam>
public class TaskBatcher<T> : IDisposable
{
    private readonly ConcurrentQueue<T> _taskQueue = new();
    private readonly Action<IEnumerable<T>> _processBatchAction;
    private readonly int _delayMilliseconds;

    // The TaskScheduler to use for executing the batch processing action
    public TaskScheduler Scheduler { get; set; }

    // 1 indicates that processing is ongoing, 0 indicates idle
    private int _isProcessing = 0;
    // cancellation token source for delaying task processing
    private CancellationTokenSource? _cancellationTokenSource;

    public TaskBatcher(
        Action<IEnumerable<T>> processBatchAction,
        int delayMilliseconds = 16,
        TaskScheduler? scheduler = null)
    {
        _processBatchAction = processBatchAction ?? throw new ArgumentNullException(nameof(processBatchAction));
        _delayMilliseconds = delayMilliseconds;
        // Default to the current synchronization context's scheduler if none is provided
        Scheduler = scheduler ?? TaskScheduler.FromCurrentSynchronizationContext();
    }

    /// <summary>
    /// Enqueue a task item for batch processing
    /// </summary>
    public void Enqueue(T taskItem)
    {
        if (taskItem == null) throw new ArgumentNullException(nameof(taskItem));

        _taskQueue.Enqueue(taskItem);
        StartProcessingCycle();
    }

    /// <summary>
    /// Process all queued tasks immediately
    /// <paramref name="useScheduler">True to use the specified TaskScheduler for processing; false to process on the current thread.</paramref>
    /// </summary>
    public void Flush()
    {
        if (_isProcessing == 0)
        {
            // No processing cycle is active; nothing to flush
            return;
        }
        _cancellationTokenSource?.Cancel();
        DoFlush();
        Interlocked.Exchange(ref _isProcessing, 0);
        DoFlush();
    }

    private void DoFlush()
    {
        if (!_taskQueue.IsEmpty)
        {
            var tasksToProcess = new List<T>();
            while (_taskQueue.TryDequeue(out var item))
            {
                tasksToProcess.Add(item);
            }

            if (tasksToProcess.Count > 0)
            {
                _processBatchAction(tasksToProcess);
            }
        }
    }

    /// <summary>
    /// Start a new processing cycle with delay
    /// </summary>
    private void StartProcessingCycle()
    {
        if (_isProcessing == 1)
        {
            // Already processing; no need to start a new cycle
            return;
        }

        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _ = Task.Delay(_delayMilliseconds, token)
                .ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        Flush();
                    }
                }, Scheduler);
        }
    }

    /// <summary>
    /// Release resources.
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _taskQueue.Clear();
    }
}