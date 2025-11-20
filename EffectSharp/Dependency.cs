using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// A class that manages dependencies between reactive variables and their subscribers.
    /// </summary>
    public class Dependency
    {
        private readonly HashSet<Effect> _subscribers = new HashSet<Effect>();

        public void AddSubscriber(Effect effect)
        {
            _subscribers.Add(effect);
        }

        public void RemoveSubscriber(Effect effect)
        {
            _subscribers.Remove(effect);
        }

        public void ClearSubscribers()
        {
            _subscribers.Clear();
        }

        public Effect Track()
        {
            var currentEffect = Effect.CurrentEffect;
            if (currentEffect != null)
            {
                AddSubscriber(currentEffect);
                DependencyTracker.DependencyTracked(this);
            }
            return currentEffect;
        }

        public void Trigger()
        {
            foreach (var subscriber in _subscribers.ToList())
            {
                subscriber.ScheduleExecution();
            }
        }
    }
}
