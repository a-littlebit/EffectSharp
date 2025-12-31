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
        private static readonly ThreadLocal<Effect?> _current = new();

        private readonly Action _action;

        private readonly Action<Effect>? _scheduler;

        private readonly AsyncLock _lock = new();

        private readonly HashSet<Dependency> _dependencies = new();

        private bool _isUntracked = false;

        private bool _isDisposed = false;

        // Allows direct access to the locked effect API during execution.
        private AsyncLock.Scope? _executionScope;

        /// <summary>
        /// Gets the effect currently being executed (tracking dependencies), if any.
        /// </summary>
        public static Effect? Current => _current.Value;

        /// <summary>
        /// Gets the custom scheduler used to enqueue this effect, if provided.
        /// </summary>
        public Action<Effect>? Scheduler => _scheduler;

        /// <summary>
        /// Indicates whether this effect has been disposed.
        /// </summary>
        public bool IsDisposed => _isDisposed;

        /// <summary>
        /// Internal lock used to synchronize effect lifecycle operations.
        /// </summary>
        public AsyncLock Lock => _lock;

        private AsyncLock.Scope? ExecutionScope => _current.Value == this ? _executionScope : null;

        /// <summary>
        /// Creates a new reactive effect that tracks dependencies and re-executes when they change.
        /// </summary>
        /// <param name="action">The effect body to execute.</param>
        /// <param name="scheduler">Optional scheduler to control execution; defaults to internal batching.</param>
        /// <param name="lazy">If false, executes immediately; if true, delays until scheduled.</param>
        public Effect(Action action, Action<Effect>? scheduler = null, bool lazy = false)
        {
            _action = action;
            _scheduler = scheduler;
            if (!lazy)
            {
                Execute();
            }
        }

        /// <summary>
        /// Executes the effect body, tracking dependencies encountered during execution.
        /// </summary>
        /// <param name="existingScope">Optional existing lock scope.</param>
        public void Execute(AsyncLock.Scope? existingScope = null)
        {
            if (_current.Value == this)
            {
                // recursive execution
                Untracked(_action);
                return;
            }

            using (var scope = _lock.Enter(existingScope))
            {
                if (_isDisposed) return;

                Stop(scope);

                var previousEffect = _current.Value;
                _current.Value = this;
                _executionScope = scope;
                try
                {
                    _action();
                }
                finally
                {
                    _executionScope = null;
                    _current.Value = previousEffect;
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
                TaskManager.QueueEffectExecution(this);
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
            var current = _current.Value;
            if (current == null || current._isUntracked)
            {
                return getter();
            }

            current._isUntracked = true;
            try
            {
                return getter();
            }
            finally
            {
                current._isUntracked = false;
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
        public void Stop(AsyncLock.Scope? existingScope = null)
        {
            using (var scope = _lock.Enter(existingScope ?? ExecutionScope))
            {
                foreach (var dependency in _dependencies)
                {
                    dependency.RemoveSubscriber(this);
                }
                _dependencies.Clear();
            }
        }

        /// <summary>
        /// Adds a dependency to the effect's tracked dependencies.
        /// Only called when locked for execution.
        /// </summary>
        internal bool AddDependency(Dependency dependency)
        {
            if (_isUntracked || _isDisposed) return false;
            return _dependencies.Add(dependency);
        }

        /// <summary>
        /// Disposes the effect, stopping execution and cleaning up subscriptions.
        /// </summary>
        public void Dispose()
        {
            Dispose(null);
        }

        /// <summary>
        /// Disposes the effect with an existing lock scope.
        /// </summary>
        /// <param name="existingScope">Existing lock scope.</param>
        public void Dispose(AsyncLock.Scope? existingScope)
        {
            using (var scope = _lock.Enter(existingScope ?? ExecutionScope))
            {
                _isDisposed = true;
                Stop(scope);
            }
        }
    }
}
