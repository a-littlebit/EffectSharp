using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<Effect, bool> _subscribers
            = new ConcurrentDictionary<Effect, bool>();

        public bool AddSubscriber(Effect effect)
        {
            return _subscribers.TryAdd(effect, true);
        }

        public bool RemoveSubscriber(Effect effect)
        {
            return _subscribers.TryRemove(effect, out _);
        }

        public Effect Track()
        {
            var currentEffect = Effect.CurrentEffect;
            if (currentEffect != null)
            {
                if (AddSubscriber(currentEffect))
                {
                    currentEffect.AddDependency(this);
                }
            }
            return currentEffect;
        }

        public void Trigger()
        {
            foreach (var subscriber in _subscribers.Keys)
            {
                subscriber.ScheduleExecution();
            }
        }
    }
}
