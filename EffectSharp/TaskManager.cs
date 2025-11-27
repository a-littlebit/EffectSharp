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
            = new TaskBatcher<Effect>(TriggerBatchEffects, 0, TaskScheduler.Default);

        private static readonly TaskBatcher<NotifyTask> _notifyBatcher
            = new TaskBatcher<NotifyTask>((tasks) => NotifyBatch(tasks), 16, TaskScheduler.FromCurrentSynchronizationContext());

        public static TaskScheduler EffectScheduler
        {
            get => _triggerBatcher.Scheduler;
            set => _triggerBatcher.Scheduler = value;
        }

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
