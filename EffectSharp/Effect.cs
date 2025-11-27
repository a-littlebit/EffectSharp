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

        private Action<Effect> _scheduler;

        private object _lock = new object();

        private volatile bool _disposed = false;

        public Action<Effect> Scheduler => _scheduler;
        public bool IsDisposed => _disposed;
        public bool Lazy { get; } = false;

        private HashSet<Dependency> _dependencies = new HashSet<Dependency>();

        public Effect(Action action, Action<Effect> scheduler = null, bool lazy = false)
        {
            _action = action;
            _scheduler = scheduler;
            Lazy = lazy;
            Execute();
        }

        public void Execute()
        {
            lock (_lock)
            {
                if (_disposed) return;

                if (Lazy)
                {
                    _action();
                    return;
                }

                ClearDependencies();
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
            if (_disposed) return;

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

        private void ClearDependencies()
        {
            foreach (var dependency in _dependencies)
            {
                dependency.RemoveSubscriber(this);
            }
            _dependencies.Clear();
        }

        internal void AddDependency(Dependency dependency)
        {
            _dependencies.Add(dependency);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                ClearDependencies();
                _disposed = true;
            }
        }
    }
}
