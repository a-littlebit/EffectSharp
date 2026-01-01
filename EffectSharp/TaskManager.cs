using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Global task manager coordinating batched processing of reactive effects and property change notifications.
    /// Provides static methods and properties for batching, scheduling, and triggering tasks.
    /// </summary>
    /// <remarks>
    /// Default configuration:
    /// <list type="bullet">
    /// <item><description><see cref="EffectBatcher"/>: queues <see cref="Effect"/> execution and processes them in batches
    /// on a scheduler (typically the current <see cref="SynchronizationContext"/>).</description></item>
    /// <item><description><see cref="NotificationBatcher"/>: queues <see cref="NotificationTask"/> (property change notifications)
    /// and uses <see cref="ITaskBatcher{Effect}.NextTick(CancellationToken)"/> from the effect batcher as its throttler,
    /// so that notifications are naturally delayed until the current effect tick has fully settled.</description></item>
    /// </list>
    /// This default coupling ensures a "compute first, notify later" ordering: updates triggered by effects are observed by
    /// property change listeners only after the corresponding effect batch has completed.
    /// Hosts can customize this behavior by calling <see cref="CreateEffectBatcherIfAbsent"/> and
    /// <see cref="CreateNotificationBatcherIfAbsent"/> early in application startup to supply their own
    /// <see cref="ITaskBatcher{T}"/> implementations or throttling strategies.
    /// </remarks>
    public static class TaskManager
    {

        private static ITaskBatcher<Effect>? _effectBatcher = null;
        private static readonly object _effectBatcherLock = new();

        private static ITaskBatcher<NotificationTask>? _notificationBatcher = null;
        private static readonly object _notificationBatcherLock = new();

        /// <summary>
        /// Gets the batcher responsible for processing queued <see cref="Effect"/> triggers.
        /// </summary>
        public static ITaskBatcher<Effect>? EffectBatcher
        {
            get
            {
                var batcher = _effectBatcher;
                if (batcher != null)
                    return batcher;

                return Volatile.Read(ref _effectBatcher);
            }
        }

        /// <summary>
        /// Gets the batcher responsible for processing queued property change notifications.
        /// </summary>
        public static ITaskBatcher<NotificationTask>? NotificationBatcher
        {
            get
            {
                var batcher = _notificationBatcher;
                if (batcher != null)
                    return batcher;

                return Volatile.Read(ref _notificationBatcher);
            }
        }

        /// <summary>
        /// Create a <see cref="ITaskBatcher{Effect}"/> for effect execution scheduling using the specified supplier function
        /// if it had not been created.
        /// </summary>
        /// <param name="supplier">Supplier invoked to create the batcher if not yet initialized.</param>
        /// <returns>true if a new effect batcher was created; otherwise, false.</returns>
        public static bool CreateEffectBatcherIfAbsent(Func<ITaskBatcher<Effect>> supplier)
        {
            if (_effectBatcher != null)
                return false;

            lock (_effectBatcherLock)
            {
                if (_effectBatcher == null)
                {
                    var created = supplier();
                    Interlocked.Exchange(ref _effectBatcher, created);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the <see cref="ITaskBatcher{Effect}"/> instance for processing effect tasks, creating a default one if it does not already exist.
        /// </summary>
        /// <returns>The singleton <see cref="ITaskBatcher{Effect}"/> instance used for batching and processing effect tasks.</returns>
        public static ITaskBatcher<Effect> GetOrCreateDefaultEffectBatcher()
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
        /// Create a <see cref="ITaskBatcher{Effect}"/> for notification batching using the specified supplier function
        /// if it has not already been created.
        /// </summary>
        /// <param name="supplier">Supplier invoked to create the batcher if not yet initialized.</param>
        /// <returns>true if the notification batcher was successfully created; otherwise, false.</returns>
        public static bool CreateNotificationBatcherIfAbsent(Func<ITaskBatcher<NotificationTask>> supplier)
        {
            if (_notificationBatcher != null)
                return false;

            lock (_notificationBatcherLock)
            {
                if (_notificationBatcher == null)
                {
                    var created = supplier();
                    Interlocked.Exchange(ref _notificationBatcher, created);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the <see cref="ITaskBatcher{NotificationTask}"/> instance for processing notification tasks,
        /// creating a default one if it does not already exist.
        /// </summary>
        /// <returns>The singleton <see cref="ITaskBatcher{NotificationTask}"/> used for batching notifications.</returns>
        public static ITaskBatcher<NotificationTask> GetOrCreateDefaultNotificationBatcher()
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
            var effectBatcher = EffectBatcher;
            if (effectBatcher != null)
                await effectBatcher.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a task that completes when all currently enqueued effects are processed.
        /// </summary>
        public static Task NextEffectTick(CancellationToken cancellationToken = default)
        {
            var effectBatcher = EffectBatcher;
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
            var notificationBatcher = NotificationBatcher;
            if (notificationBatcher != null)
                await notificationBatcher.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a task that completes when all currently enqueued notifications are processed.
        /// </summary>
        public static Task NextNotificationTick(CancellationToken cancellationToken = default)
        {
            var notificationBatcher = NotificationBatcher;
            cancellationToken.ThrowIfCancellationRequested();
            return notificationBatcher != null ? notificationBatcher.NextTick(cancellationToken) : Task.CompletedTask;
        }

        /// <summary>
        /// Default processor that invokes each notification action.
        /// </summary>
        public static void DefaultNotificationBatchProcessor(List<NotificationTask> tasks)
        {
            var notificationSet = new HashSet<NotificationTask>(tasks);
            foreach (var task in notificationSet)
            {
                var args = new PropertyChangedEventArgs(task.PropertyName);
                task.Notifier(args);
            }
        }
    }

    /// <summary>
    /// Interface for a batched task scheduler that supports enqueuing tasks, flushing, and awaiting completion.
    /// </summary>
    /// <typeparam name="T">Type of task.</typeparam>
    public interface ITaskBatcher<T>
    {
        /// <summary>
        /// Enqueue a task for batched execution.
        /// </summary>
        /// <param name="task">The task to enqueue.</param>
        void Enqueue(T task);

        /// <summary>
        /// Asynchronously flush all currently enqueued tasks.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task FlushAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a task that completes when all currently enqueued tasks are processed.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task NextTick(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a property change notification task.
    /// </summary>
    public readonly struct NotificationTask : IEquatable<NotificationTask>
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

        public bool Equals(NotificationTask other)
        {
            return ReferenceEquals(Model, other.Model) &&
                   PropertyName == other.PropertyName;
        }

        public override bool Equals(object obj)
        {
            return obj is NotificationTask task &&
                   Equals(task);
        }

        public override int GetHashCode()
        {
            int modelHash = Model == null ? 0 : RuntimeHelpers.GetHashCode(Model);
            int propertyHash = PropertyName == null ? 0 : PropertyName.GetHashCode();
            unchecked
            {
                return ((modelHash << 5) + modelHash) ^ propertyHash;
            }
        }
    }
}
