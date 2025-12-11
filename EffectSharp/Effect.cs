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
        internal static readonly AsyncLocal<Effect> CurrentEffectContext = new AsyncLocal<Effect>();
        public static Effect CurrentEffect => CurrentEffectContext.Value;

        private readonly Action _action;

        private readonly Action<Effect> _scheduler;

        private volatile bool _isDisposed = false;

        private AsyncLock _lock = new AsyncLock();

        public Action<Effect> Scheduler => _scheduler;
        public bool IsDisposed => _isDisposed;

        private HashSet<Dependency> _dependencies = new HashSet<Dependency>();

        public Effect(Action action, Action<Effect> scheduler = null, bool lazy = false)
        {
            _action = action;
            _scheduler = scheduler;
            if (!lazy)
            {
                Execute();
            }
        }

        public AsyncLock Lock => _lock;

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

        public static void Untracked(Action action)
        {
            Untracked(() =>
            {
                action();
                return true;
            });
        }

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
