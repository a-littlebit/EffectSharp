using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

        private static volatile TaskBatcher<Effect>? _effectBatcher = null;
        private static readonly object _effectBatcherLock = new();

        private static volatile TaskBatcher<NotificationTask>? _notificationBatcher = null;
        private static readonly object _notificationBatcherLock = new();

        /// <summary>
        /// Gets the batcher responsible for processing queued <see cref="Effect"/> triggers.
        /// </summary>
        public static TaskBatcher<Effect>? EffectBatcher => _effectBatcher;
        /// <summary>
        /// Gets the batcher responsible for processing queued property change notifications.
        /// </summary>
        public static TaskBatcher<NotificationTask>? NotificationBatcher => _notificationBatcher;

        /// <summary>
        /// Create a <see cref="TaskBatcher{Effect}"/> for effect execution scheduling using the specified supplier function
        /// if it had not been created.
        /// </summary>
        /// <param name="supplier">Supplier invoked to create the batcher if not yet initialized.</param>
        /// <returns>true if a new effect batcher was created; otherwise, false.</returns>
        public static bool CreateEffectBatcherIfAbsent(Func<TaskBatcher<Effect>> supplier)
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
            CreateEffectBatcherIfAbsent(() =>
            {
                var batcher = new TaskBatcher<Effect>(
                    batchProcessor: DefaultEffectBatchProcessor,
                    intervalMs: 0,
                    scheduler: SynchronizationContext.Current == null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext(),
                    maxConsumers: 1);
                batcher.BatchProcessingFailed += TraceEffectFailure;
                return batcher;
            });
            return _effectBatcher!;
        }

        /// <summary>
        /// Default tracer for effect batch processing failures.
        /// </summary>
        public static void TraceEffectFailure(object sender, BatchProcessingFailedEventArgs<Effect> e)
        {
            System.Diagnostics.Trace.TraceError($"Effect batch processing failed: {e.Exception}");
        }

        /// <summary>
        /// Create a <see cref="TaskBatcher{Effect}"/> for notification batching using the specified supplier function
        /// if it has not already been created.
        /// </summary>
        /// <param name="supplier">Supplier invoked to create the batcher if not yet initialized.</param>
        /// <returns>true if the notification batcher was successfully created; otherwise, false.</returns>
        public static bool CreateNotificationBatcherIfAbsent(Func<TaskBatcher<NotificationTask>> supplier)
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
            CreateNotificationBatcherIfAbsent(() =>
            {
                var effectBatcher = GetOrCreateDefaultEffectBatcher();
                var batcher = new TaskBatcher<NotificationTask>(
                    batchProcessor: DefaultNotificationBatchProcessor,
                    throttler: effectBatcher.NextTick,
                    scheduler: SynchronizationContext.Current == null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext(),
                    maxConsumers: 1);
                batcher.BatchProcessingFailed += TraceNotificationFailure;
                return batcher;
            });
            return _notificationBatcher!;
        }

        /// <summary>
        /// Default tracer for notification batch processing failures.
        /// </summary>
        public static void TraceNotificationFailure(object sender, BatchProcessingFailedEventArgs<NotificationTask> e)
        {
            System.Diagnostics.Trace.TraceError($"Notification batch processing failed: {e.Exception}");
        }

        /// <summary>
        /// Enqueues an effect for batched execution.
        /// </summary>
        public static void QueueEffectExecution(Effect effect)
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
        public static Task NextEffectTick(CancellationToken cancellationToken = default)
        {
            var effectBatcher = _effectBatcher;
            cancellationToken.ThrowIfCancellationRequested();
            return effectBatcher != null ? effectBatcher.NextTick(cancellationToken) : Task.CompletedTask;
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
        }

        /// <summary>
        /// Enqueues a property change notification task.
        /// </summary>
        public static void QueueNotification(object model, string propertyName, Action<PropertyChangedEventArgs> notifier)
        {
            var task = new NotificationTask(model, propertyName, notifier);
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
        public static Task NextNotificationTick(CancellationToken cancellationToken = default)
        {
            var notificationBatcher = _notificationBatcher;
            cancellationToken.ThrowIfCancellationRequested();
            return notificationBatcher != null ? notificationBatcher.NextTick(cancellationToken) : Task.CompletedTask;
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
        public object Model { get; }
        /// <summary>
        /// The name of the property that changed.
        /// </summary>
        public string PropertyName { get; }
        /// <summary>
        /// The action to invoke with the <see cref="PropertyChangedEventArgs"/>.
        /// </summary>
        public Action<PropertyChangedEventArgs> Notifier { get; }

        /// <summary>
        /// Initializes a new instance of the NotificationTask class to handle property change notifications for a
        /// specified model and property.
        /// </summary>
        /// <param name="model">The object instance whose property changes are being monitored. Cannot be null.</param>
        /// <param name="propertyName">The name of the property to observe for changes. Cannot be null or empty.</param>
        /// <param name="notifier">
        /// An action delegate that is invoked when the specified property changes. Receives a PropertyChangedEventArgs
        /// describing the change. Cannot be null.
        /// </param>
        public NotificationTask(object model, string propertyName, Action<PropertyChangedEventArgs> notifier)
        {
            Model = model;
            PropertyName = propertyName;
            Notifier = notifier;
        }
    }
}
