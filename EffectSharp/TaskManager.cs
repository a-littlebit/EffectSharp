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
    /// Provides static methods and properties for batching, scheduling, and triggering effect and notification tasks
    /// within the application.
    /// </summary>
    public static class TaskManager
    {

        private static readonly TaskBatcher<Effect> _effectBatcher
            = new TaskBatcher<Effect>(TriggerBatchEffects, 0, TaskScheduler.FromCurrentSynchronizationContext());

        private static readonly TaskBatcher<NotificationTask> _notificationBatcher
            = new TaskBatcher<NotificationTask>(NotifyBatch, 16, TaskScheduler.FromCurrentSynchronizationContext());

        private static volatile bool _flushNotificationAfterEffectBatch = true;

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
            get => _effectBatcher.IntervalMs;
            set => _effectBatcher.IntervalMs = value;
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
            get => _effectBatcher.Scheduler;
            set => _effectBatcher.Scheduler = value;
        }

        /// <summary>
        /// Occurs when a batch of effect processing fails.
        /// </summary>
        public static event EventHandler<BatchProcessingFailedEventArgs<Effect>> EffectFailed
        {
            add => _effectBatcher.BatchProcessingFailed += value;
            remove => _effectBatcher.BatchProcessingFailed -= value;
        }

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
            get => _notificationBatcher.IntervalMs;
            set => _notificationBatcher.IntervalMs = value;
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
            get => _notificationBatcher.Scheduler;
            set => _notificationBatcher.Scheduler = value;
        }

        /// <summary>
        /// Occurs when a batch of notification fails.
        /// </summary>
        public static event EventHandler<BatchProcessingFailedEventArgs<NotificationTask>> NotificationFailed
        {
            add => _notificationBatcher.BatchProcessingFailed += value;
            remove => _notificationBatcher.BatchProcessingFailed -= value;
        }

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

        static TaskManager()
        {
            EffectFailed += TraceEffectFailure;
            NotificationFailed += TraceNotificationFailure;
        }

        public static void EnqueueEffectTrigger(Effect effect)
        {
            _effectBatcher.Enqueue(effect);
        }

        public static async Task FlushEffectQueue()
        {
            await _effectBatcher.FlushAsync().ConfigureAwait(false);
        }

        public static Task NextEffectTick()
        {
            return _effectBatcher.NextTick();
        }

        public static async Task TriggerBatchEffects(List<Effect> effects)
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
                _ = _notificationBatcher.FlushAsync();
            }
        }

        public static void EnqueueNotification(object model, string propertyName, Action<PropertyChangedEventArgs> notifier)
        {
            var task = new NotificationTask
            {
                Model = model,
                PropertyName = propertyName,
                Notifier = notifier
            };
            _notificationBatcher.Enqueue(task);
        }

        public static async Task FlushNotificationQueue()
        {
            await _notificationBatcher.FlushAsync().ConfigureAwait(false);
        }

        public static Task NextNotificationTick()
        {
            return _notificationBatcher.NextTick();
        }

        private static void NotifyBatch(List<NotificationTask> tasks)
        {
            var grouped = tasks.GroupBy(t => (t.Model, t.PropertyName));
            foreach (var group in grouped)
            {
                var (_, propName) = group.Key;
                group.First().Notifier(new PropertyChangedEventArgs(propName));
            }
        }
    }

    public class NotificationTask
    {
        public object Model { get; set; }
        public string PropertyName { get; set; }
        public Action<PropertyChangedEventArgs> Notifier { get; set; }
    }
}
