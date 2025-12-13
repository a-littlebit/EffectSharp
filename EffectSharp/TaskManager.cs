using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Global task manager coordinating batched processing of reactive effects and property change notifications.
    /// Provides static methods and properties for batching, scheduling, and triggering tasks.
    /// </summary>
    public static class TaskManager
    {

        private static volatile TaskBatcher<Effect> _effectBatcher = null;
        private static readonly object _effectBatcherLock = new object();

        private static volatile TaskBatcher<NotificationTask> _notificationBatcher = null;
        private static readonly object _notificationBatcherLock = new object();

        private static volatile bool _flushNotificationAfterEffectBatch = true;

        /// <summary>
        /// Gets the batcher responsible for processing queued <see cref="Effect"/> triggers.
        /// </summary>
        public static TaskBatcher<Effect> EffectBatcher => _effectBatcher;
        /// <summary>
        /// Gets the batcher responsible for processing queued property change notifications.
        /// </summary>
        public static TaskBatcher<NotificationTask> NotificationBatcher => _notificationBatcher;

        /// <summary>
        /// Attempts to initialize the <see cref="TaskBatcher{Effect}"/> using the specified supplier function
        /// if it has not already been created.
        /// </summary>
        /// <param name="supplier">Supplier invoked to create the batcher if not yet initialized.</param>
        /// <returns>true if the effect batcher was successfully created; otherwise, false.</returns>
        public static bool TryCreateEffectBatcher(Func<TaskBatcher<Effect>> supplier)
        {
            if (_effectBatcher == null)
            {
                lock (_effectBatcherLock)
                {
                    if (_effectBatcher == null)
                    {
                        _effectBatcher = supplier();
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the <see cref="TaskBatcher{Effect}"/> instance for processing effect tasks, creating a default one if it does not already exist.
        /// </summary>
        /// <returns>The singleton <see cref="TaskBatcher{Effect}"/> instance used for batching and processing effect tasks.</returns>
        public static TaskBatcher<Effect> GetOrCreateDefaultEffectBatcher()
        {
            TryCreateEffectBatcher(() =>
            {
                var batcher = new TaskBatcher<Effect>(
                    batchProcessor: DefaultEffectBatchProcessor,
                    intervalMs: 0,
                    scheduler: SynchronizationContext.Current == null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext(),
                    maxConsumers: 1);
                batcher.BatchProcessingFailed += TraceEffectFailure;
                return batcher;
            });
            return _effectBatcher;
        }

        /// <summary>
        /// Attempts to initialize the <see cref="TaskBatcher{NotificationTask}"/> using the specified supplier function
        /// if it has not already been created.
        /// </summary>
        /// <param name="supplier">Supplier invoked to create the batcher if not yet initialized.</param>
        /// <returns>true if the notification batcher was successfully created; otherwise, false.</returns>
        public static bool TryCreateNotificationBatcher(Func<TaskBatcher<NotificationTask>> supplier)
        {
            if (_notificationBatcher == null)
            {
                lock (_notificationBatcherLock)
                {
                    if (_notificationBatcher == null)
                    {
                        _notificationBatcher = supplier();
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the <see cref="TaskBatcher{NotificationTask}"/> instance for processing notification tasks,
        /// creating a default one if it does not already exist.
        /// </summary>
        /// <returns>The singleton <see cref="TaskBatcher{NotificationTask}"/> used for batching notifications.</returns>
        public static TaskBatcher<NotificationTask> GetOrCreateDefaultNotificationBatcher()
        {
            TryCreateNotificationBatcher(() =>
            {
                var batcher = new TaskBatcher<NotificationTask>(
                    batchProcessor: DefaultNotificationBatchProcessor,
                    intervalMs: 16,
                    scheduler: SynchronizationContext.Current == null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext(),
                    maxConsumers: 1);
                batcher.BatchProcessingFailed += TraceNotificationFailure;
                return batcher;
            });
            return _notificationBatcher;
        }

        /// <summary>
        /// Gets or sets the interval, in milliseconds, between effect trigger batches.
        /// </summary>
        /// <remarks>
        /// Setting a lower value may increase responsiveness but can result in higher resource
        /// usage. The interval must be a non-negative integer.
        /// Default is 0 ms, meaning effects are processed as soon as possible.
        /// </remarks>
        public static int EffectIntervalMs
        {
            get => GetOrCreateDefaultEffectBatcher().IntervalMs;
            set => GetOrCreateDefaultEffectBatcher().IntervalMs = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="TaskScheduler"/> used to schedule effect-related tasks.
        /// </summary>
        /// <remarks>
        /// Changing this property affects how effect tasks are dispatched and may impact
        /// concurrency or execution order. Ensure that the assigned <see cref="TaskScheduler"/> is appropriate for the
        /// application's threading model.
        /// </remarks>
        public static TaskScheduler EffectTaskScheduler
        {
            get => GetOrCreateDefaultEffectBatcher().Scheduler;
            set => GetOrCreateDefaultEffectBatcher().Scheduler = value;
        }

        /// <summary>
        /// Occurs when a batch of effect processing fails.
        /// </summary>
        public static event EventHandler<BatchProcessingFailedEventArgs<Effect>> EffectFailed
        {
            add => GetOrCreateDefaultEffectBatcher().BatchProcessingFailed += value;
            remove => GetOrCreateDefaultEffectBatcher().BatchProcessingFailed -= value;
        }

        /// <summary>
        /// Default tracer for effect batch processing failures.
        /// </summary>
        public static void TraceEffectFailure(object sender, BatchProcessingFailedEventArgs<Effect> e)
        {
            System.Diagnostics.Trace.TraceError($"Effect batch processing failed: {e.Exception}");
        }

        /// <summary>
        /// Gets or sets the interval, in milliseconds, between UI notification batches.
        /// </summary>
        /// <remarks>
        /// Setting a lower value may increase UI responsiveness but can result in higher resource
        /// usage. The interval must be a non-negative integer.
        /// Default is 16 ms, aligning with a typical 60Hz UI refresh rate.
        /// </remarks>
        public static int NotificationIntervalMs
        {
            get => GetOrCreateDefaultNotificationBatcher().IntervalMs;
            set => GetOrCreateDefaultNotificationBatcher().IntervalMs = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="TaskScheduler"/> used to schedule UI notification tasks.
        /// </summary>
        /// <remarks>
        /// Changing this property affects how UI notification tasks are dispatched and may impact
        /// concurrency or execution order. Ensure that the assigned <see cref="TaskScheduler"/> is appropriate for the
        /// UI threading model.
        /// </remarks>
        public static TaskScheduler NotificationTaskScheduler
        {
            get => GetOrCreateDefaultNotificationBatcher().Scheduler;
            set => GetOrCreateDefaultNotificationBatcher().Scheduler = value;
        }

        /// <summary>
        /// Occurs when a batch of notification fails.
        /// </summary>
        public static event EventHandler<BatchProcessingFailedEventArgs<NotificationTask>> NotificationFailed
        {
            add => GetOrCreateDefaultNotificationBatcher().BatchProcessingFailed += value;
            remove => GetOrCreateDefaultNotificationBatcher().BatchProcessingFailed -= value;
        }

        /// <summary>
        /// Default tracer for notification batch processing failures.
        /// </summary>
        public static void TraceNotificationFailure(object sender, BatchProcessingFailedEventArgs<NotificationTask> e)
        {
            System.Diagnostics.Trace.TraceError($"Notification batch processing failed: {e.Exception}");
        }

        /// <summary>
        /// Gets or sets a value indicating whether to automatically flush the notification queue
        /// after each effect execution batch. Default is true.
        /// </summary>
        /// <remarks>
        /// Enabling this option ensures that UI updates occur promptly after effects are executed,
        /// but may increase the frequency of UI updates. Disable this option if you want to
        /// manually control when the notification queue is flushed.
        /// </remarks>
        public static bool FlushNotificationAfterEffectBatch
        {
            get => _flushNotificationAfterEffectBatch;
            set => _flushNotificationAfterEffectBatch = value;
        }

        /// <summary>
        /// Enqueues an effect for batched execution.
        /// </summary>
        public static void EnqueueEffectTrigger(Effect effect)
        {
            GetOrCreateDefaultEffectBatcher().Enqueue(effect);
        }

        /// <summary>
        /// Asynchronously flushes the effect queue.
        /// </summary>
        public static async Task FlushEffectQueue()
        {
            var effectBatcher = _effectBatcher;
            if (effectBatcher != null)
                await effectBatcher.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a task that completes when all currently enqueued effects are processed.
        /// </summary>
        public static Task NextEffectTick()
        {
            var effectBatcher = _effectBatcher;
            return effectBatcher != null ? effectBatcher.NextTick() : Task.CompletedTask;
        }

        /// <summary>
        /// Default processor that executes each effect immediately on the current thread.
        /// </summary>
        public static async Task DefaultEffectBatchProcessor(List<Effect> effects)
        {
            var uniqueEffects = new HashSet<Effect>(effects);
            foreach (var effect in uniqueEffects)
            {
                using (var scope = await effect.Lock.EnterAsync())
                {
                    effect.Execute(scope);
                }
            }

            if (FlushNotificationAfterEffectBatch)
            {
                var notificationBatcher = _notificationBatcher;
                _ = notificationBatcher?.FlushAsync();
            }
        }

        /// <summary>
        /// Enqueues a property change notification task.
        /// </summary>
        public static void EnqueueNotification(object model, string propertyName, Action<PropertyChangedEventArgs> notifier)
        {
            var task = new NotificationTask
            {
                Model = model,
                PropertyName = propertyName,
                Notifier = notifier
            };
            GetOrCreateDefaultNotificationBatcher().Enqueue(task);
        }

        /// <summary>
        /// Asynchronously flushes the notification queue.
        /// </summary>
        public static async Task FlushNotificationQueue()
        {
            var notificationBatcher = _notificationBatcher;
            if (notificationBatcher != null)
                await notificationBatcher.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a task that completes when all currently enqueued notifications are processed.
        /// </summary>
        public static Task NextNotificationTick()
        {
            var notificationBatcher = _notificationBatcher;
            return notificationBatcher != null ? notificationBatcher.NextTick() : Task.CompletedTask;
        }

        /// <summary>
        /// Default processor that invokes each notification action.
        /// </summary>
        public static void DefaultNotificationBatchProcessor(List<NotificationTask> tasks)
        {
            var grouped = tasks.GroupBy(t => (t.Model, t.PropertyName));
            foreach (var group in grouped)
            {
                var (_, propName) = group.Key;
                group.First().Notifier(new PropertyChangedEventArgs(propName));
            }
        }
    }

    /// <summary>
    /// Represents a property change notification task.
    /// </summary>
    public class NotificationTask
    {
        /// <summary>
        /// The object whose property changed.
        /// </summary>
        public object Model { get; set; }
        /// <summary>
        /// The name of the property that changed.
        /// </summary>
        public string PropertyName { get; set; }
        /// <summary>
        /// The action to invoke with the <see cref="PropertyChangedEventArgs"/>.
        /// </summary>
        public Action<PropertyChangedEventArgs> Notifier { get; set; }
    }
}
