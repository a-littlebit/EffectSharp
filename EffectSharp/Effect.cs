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

        public Action<Effect> Scheduler { get; set; } = null;
        public bool Lazy { get; } = false;

        private HashSet<Dependency> _dependencies = null;

        public Effect(Action action, Action<Effect> scheduler = null, bool lazy = false)
        {
            _action = action;
            Scheduler = scheduler;
            Lazy = lazy;
            Execute();
        }

        public void Execute()
        {
            if (Lazy)
            {
                _action();
                return;
            }

            Dispose();
            var previousEffect = CurrentEffectContext.Value;
            CurrentEffectContext.Value = this;
            DependencyTracker.StartDependencyTracking();
            try
            {
                _action();
            }
            finally
            {
                _dependencies = DependencyTracker.RetrieveAndClearTrackedDependencies();
                CurrentEffectContext.Value = previousEffect;
            }
        }

        public void ScheduleExecution()
        {
            if (Scheduler != null)
            {
                Scheduler(this);
            }
            else
            {
                Execute();
            }
        }

        public void Dispose()
        {
            if (_dependencies != null)
            {
                foreach (var dep in _dependencies)
                {
                    dep.RemoveSubscriber(this);
                }
                _dependencies.Clear();
                _dependencies = null;
            }
        }
    }
}
