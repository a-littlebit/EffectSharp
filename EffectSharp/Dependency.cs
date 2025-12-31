using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// A class that manages dependencies between reactive variables and their subscribers.
    /// </summary>
    public class Dependency
    {
        private ConcurrentDictionary<Effect, bool>? _subscribers;

        private ConcurrentDictionary<Effect, bool> Subscribers
        {
            get
            {
                var s = _subscribers;
                if (s != null) return s;

                var created = new ConcurrentDictionary<Effect, bool>();
                return Interlocked.CompareExchange(ref _subscribers, created, null) ?? created;
            }
        }

        /// <summary>
        /// Adds an effect as a subscriber to this dependency.
        /// </summary>
        /// <param name="effect">The effect to subscribe.</param>
        /// <returns>True if added; false if already present.</returns>
        public bool AddSubscriber(Effect effect)
        {
            return Subscribers.TryAdd(effect, true);
        }

        /// <summary>
        /// Removes an effect from the subscribers of this dependency.
        /// </summary>
        /// <param name="effect">The effect to remove.</param>
        /// <returns>True if removed; false if not found.</returns>
        public bool RemoveSubscriber(Effect effect)
        {
            return Subscribers.TryRemove(effect, out _);
        }

        /// <summary>
        /// Tracks the current effect, subscribing it to this dependency if present.
        /// </summary>
        /// <returns>The current effect or null if none.</returns>
        public Effect? Track()
        {
            var currentEffect = Effect.Current;
            if (currentEffect != null && currentEffect.AddDependency(this))
            {
                AddSubscriber(currentEffect);
            }
            return currentEffect;
        }

        /// <summary>
        /// Triggers all subscribers to schedule re-execution.
        /// </summary>
        public void Trigger()
        {
            foreach (var subscriber in Subscribers.Keys)
            {
                subscriber.ScheduleExecution();
            }
        }
    }
}
