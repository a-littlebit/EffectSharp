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

        private static readonly TaskBatcher<Effect> _triggerBatcher
            = new TaskBatcher<Effect>(TriggerBatchEffects, 0, TaskScheduler.FromCurrentSynchronizationContext());

        private static readonly TaskBatcher<NotifyTask> _notifyBatcher
            = new TaskBatcher<NotifyTask>(NotifyBatch, 16, TaskScheduler.FromCurrentSynchronizationContext());

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
            get => _triggerBatcher.IntervalMs;
            set => _triggerBatcher.IntervalMs = value;
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
            get => _triggerBatcher.Scheduler;
            set => _triggerBatcher.Scheduler = value;
        }

        /// <summary>
        /// Gets or sets the interval, in milliseconds, between UI notification batches.
        /// </summary>
        /// <remarks>
        /// Setting a lower value may increase UI responsiveness but can result in higher resource
        /// usage. The interval must be a non-negative integer.
        /// Default is 16 ms, aligning with a typical 60Hz UI refresh rate.
        /// </remarks>
        public static int NotifyIntervalMs
        {
            get => _notifyBatcher.IntervalMs;
            set => _notifyBatcher.IntervalMs = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="TaskScheduler"/> used to schedule UI notification tasks.
        /// </summary>
        /// <remarks>
        /// Changing this property affects how UI notification tasks are dispatched and may impact
        /// concurrency or execution order. Ensure that the assigned <see cref="TaskScheduler"/> is appropriate for the
        /// UI threading model.
        /// </remarks>
        public static TaskScheduler NotifyTaskScheduler
        {
            get => _notifyBatcher.Scheduler;
            set => _notifyBatcher.Scheduler = value;
        }

        public static void EnqueueEffectTrigger(Effect effect)
        {
            _triggerBatcher.Enqueue(effect);
        }

        public static async Task FlushEffectQueue()
        {
            await _triggerBatcher.FlushAsync();
        }

        public static Task NextEffectTick()
        {
            return _triggerBatcher.NextTick();
        }

        public static void TriggerBatchEffects(IEnumerable<Effect> effects)
        {
            var uniqueEffects = new HashSet<Effect>(effects);
            foreach (var effect in uniqueEffects)
            {
                effect.Execute();
            }
        }

        public static void EnqueueNotify(object model, string propertyName, Action<PropertyChangedEventArgs> notifier)
        {
            var task = new NotifyTask
            {
                Model = model,
                PropertyName = propertyName,
                Notifier = notifier
            };
            _notifyBatcher.Enqueue(task);
        }

        public static async Task FlushNotifyQueue()
        {
            await _notifyBatcher.FlushAsync();
        }

        public static Task NextNotifyTick()
        {
            return _notifyBatcher.NextTick();
        }

        private static void NotifyBatch(IEnumerable<NotifyTask> tasks)
        {
            var grouped = tasks.GroupBy(t => (t.Model, t.PropertyName));
            foreach (var group in grouped)
            {
                var (_, propName) = group.Key;
                group.First().Notifier(new PropertyChangedEventArgs(propName));
            }
        }
    }

    internal class NotifyTask
    {
        internal object Model { get; set; }
        internal string PropertyName { get; set; }
        internal Action<PropertyChangedEventArgs> Notifier { get; set; }
    }
}
