using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Reactive Effect: Tracks dependency changes and re-executes the associated action.
    /// </summary>
    public class Effect : IDisposable
    {
        internal static readonly ThreadLocal<Effect> CurrentEffectContext = new ThreadLocal<Effect>();

        /// <summary>
        /// Gets the effect currently being executed (tracking dependencies), if any.
        /// </summary>
        public static Effect CurrentEffect => CurrentEffectContext.Value;

        private readonly Action _action;

        private readonly Action<Effect> _scheduler;

        private volatile bool _isDisposed = false;

        private readonly AsyncLock _lock = new AsyncLock();

        /// <summary>
        /// Gets the custom scheduler used to enqueue this effect, if provided.
        /// </summary>
        public Action<Effect> Scheduler => _scheduler;
        /// <summary>
        /// Indicates whether this effect has been disposed.
        /// </summary>
        public bool IsDisposed => _isDisposed;

        private readonly HashSet<Dependency> _dependencies = new HashSet<Dependency>();

        /// <summary>
        /// Creates a new reactive effect that tracks dependencies and re-executes when they change.
        /// </summary>
        /// <param name="action">The effect body to execute.</param>
        /// <param name="scheduler">Optional scheduler to control execution; defaults to internal batching.</param>
        /// <param name="lazy">If false, executes immediately; if true, delays until scheduled.</param>
        public Effect(Action action, Action<Effect> scheduler = null, bool lazy = false)
        {
            _action = action;
            _scheduler = scheduler;
            if (!lazy)
            {
                Execute();
            }
        }

        /// <summary>
        /// Internal lock used to synchronize effect lifecycle operations.
        /// </summary>
        public AsyncLock Lock => _lock;

        /// <summary>
        /// Executes the effect body, tracking dependencies encountered during execution.
        /// </summary>
        /// <param name="existingScope">Optional existing lock scope.</param>
        public void Execute(AsyncLock.Scope existingScope = null)
        {
            using (var scope = _lock.Enter(existingScope))
            {
                if (_isDisposed) return;

                Stop(scope);

                var previousEffect = CurrentEffectContext.Value;
                CurrentEffectContext.Value = this;
                try
                {
                    _action();
                }
                finally
                {
                    CurrentEffectContext.Value = previousEffect;
                }
            }
        }

        /// <summary>
        /// Schedules the effect for execution using the provided scheduler or the default task manager.
        /// </summary>
        public void ScheduleExecution()
        {
            if (_isDisposed) return;

            var scheduler = Scheduler;
            if (scheduler != null)
            {
                scheduler(this);
            }
            else
            {
                TaskManager.EnqueueEffectTrigger(this);
            }
        }

        /// <summary>
        /// Executes the provided function without dependency tracking.
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="getter">Function to execute untracked.</param>
        /// <returns>Function result.</returns>
        public static T Untracked<T>(Func<T> getter)
        {
            var previousEffect = CurrentEffectContext.Value;
            if (previousEffect == null)
            {
                return getter();
            }

            CurrentEffectContext.Value = null;
            try
            {
                return getter();
            }
            finally
            {
                CurrentEffectContext.Value = previousEffect;
            }
        }

        /// <summary>
        /// Executes the provided action without dependency tracking.
        /// </summary>
        /// <param name="action">Action to execute untracked.</param>
        public static void Untracked(Action action)
        {
            Untracked(() =>
            {
                action();
                return true;
            });
        }

        /// <summary>
        /// Stops the effect and unsubscribes from all tracked dependencies.
        /// </summary>
        /// <param name="existingScope">Optional existing lock scope.</param>
        public void Stop(AsyncLock.Scope existingScope = null)
        {
            using (var scope = _lock.Enter(existingScope))
            {
                foreach (var dependency in _dependencies)
                {
                    dependency.RemoveSubscriber(this);
                }
                _dependencies.Clear();
            }
        }

        internal void AddDependency(Dependency dependency)
        {
            _dependencies.Add(dependency);
        }

        /// <summary>
        /// Disposes the effect, stopping execution and cleaning up subscriptions.
        /// </summary>
        public void Dispose()
        {
            using (var scope = _lock.Enter())
            {
                if (_isDisposed) return;
                Stop(scope);
                _isDisposed = true;
            }
        }
    }
}
