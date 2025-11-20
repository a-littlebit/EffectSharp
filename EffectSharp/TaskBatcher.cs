using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// A generic task batcher that queues multiple task items and
    /// executes a processing function to handle all queued tasks at once after a specified delay.
    /// This helps to reduce the performance overhead caused by frequent operations, such as merging multiple UI updates.
    /// </summary>
    /// <typeparam name="T">Data type processed by the task. </typeparam>
    public class TaskBatcher<T> : IDisposable
    {
        private readonly ConcurrentQueue<T> _taskQueue = new ConcurrentQueue<T>();
        private readonly Action<IEnumerable<T>> _processBatchAction;
        private readonly int _delayMilliseconds;

        // The TaskScheduler to use for executing the batch processing action
        public TaskScheduler Scheduler { get; set; }

        // 0 - idle; 1 - processing; -1 - disposed
        private int _state = 0;

        // number of currently working consumers
        private int _workingCount = 0;

        // cancellation token source for delaying task processing
        private CancellationTokenSource _cancellationTokenSource;

        public TaskBatcher(
            Action<IEnumerable<T>> processBatchAction,
            int delayMilliseconds = 16,
            TaskScheduler scheduler = null)
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
        /// </summary>
        public void Flush(bool waitForComplete = true)
        {
            if (_state == -1 || _taskQueue.IsEmpty)
            {
                // No processing cycle is active; nothing to flush
                return;
            }
            // Cancel any pending delayed processing
            _cancellationTokenSource?.Cancel();
            // Set state to idle so that a new processing cycle can be started later
            Interlocked.Exchange(ref _state, 0);
            // Indicate that a worker is active
            Interlocked.Increment(ref _workingCount);
            // Do the actual flushing
            DoFlush();
            // Indicate that the worker is done
            lock (_taskQueue)
            {
                var remainingWorkers = Interlocked.Decrement(ref _workingCount);
                if (remainingWorkers == 0)
                {
                    Monitor.PulseAll(_taskQueue);
                }
                else if (waitForComplete)
                {
                    Monitor.Wait(_taskQueue);
                }
            }
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
            if (_state == 1)
            {
                // Already started.
                return;
            }
            else if (_state == -1)
            {
                // Already disposed.
                throw new ObjectDisposedException(nameof(TaskBatcher<T>));
            }

            if (_taskQueue.IsEmpty)
            {
                // No tasks to process.
                return;
            }

            if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;

                _ = Task.Delay(_delayMilliseconds, token)
                    .ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                        {
                            Flush(false);
                        }
                    }, Scheduler);
            }
        }

        /// <summary>
        /// Release resources.
        /// </summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref _state, -1);
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            // _taskQueue.Clear();
        }
    }
}