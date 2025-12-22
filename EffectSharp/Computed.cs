using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Represents a computed reactive value that automatically updates when its dependencies change.
    /// </summary>
    /// <typeparam name="T">The type of the computed value. </typeparam>
    public class Computed<T> : INotifyPropertyChanging, INotifyPropertyChanged, IReactive, IRef<T>, IReadOnlyRef<T>, IDisposable
    {
        private readonly IAtomic<T> _value;
        private int _dirtyFlag = 2; // 0: clean, 1: computing, 2: dirty
        private readonly Action<T>? _setter;
        private readonly Dependency _dependency = new();
        private readonly Effect _effect;

        /// <summary>
        /// Raised before the computed <see cref="Value"/> changes.
        /// </summary>
        public event PropertyChangingEventHandler? PropertyChanging;
        /// <summary>
        /// Raised after the computed <see cref="Value"/> has changed.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates a computed reactive value.
        /// </summary>
        /// <param name="getter">Function that computes the value based on tracked dependencies.</param>
        /// <param name="setter">Optional setter to support assignment to <see cref="Value"/>.</param>
        public Computed(Func<T> getter, Action<T>? setter = null)
        {
            _value = AtomicFactory<T>.Create();
            _setter = setter;
            _effect = new Effect(() => Validate(getter), (_) => Invalidate(), true);
        }

        private void Validate(Func<T> getter)
        {
            Interlocked.CompareExchange(ref _dirtyFlag, 1, 2);

            // When entering this method, we must recompute the value.
            // Or the effect will lose track of dependencies if we skip computation.
            _value.Value = getter();

            Interlocked.CompareExchange(ref _dirtyFlag, 0, 1);
        }

        /// <summary>
        /// Gets or sets the computed value. Getting participates in dependency tracking and recomputation when dirty.
        /// Setting delegates to the provided setter if available; otherwise throws for read-only computed values.
        /// </summary>
        public T Value
        {
            get
            {
                _dependency.Track();

                if (Volatile.Read(ref _dirtyFlag) != 0)
                {
                    using (var scope = _effect.Lock.Enter())
                    {
                        if (Volatile.Read(ref _dirtyFlag) != 0)
                        {
                            _effect.Execute(scope);
                        }
                    }
                }

                return _value.Value;
            }
            set
            {
                if (_setter == null)
                    throw new InvalidOperationException("This computed property is read-only.");
                _setter(value);
            }
        }

        /// <summary>
        /// Marks the computed value as dirty and triggers notifications.
        /// </summary>
        public void Invalidate()
        {
            Interlocked.Exchange(ref _dirtyFlag, 2);
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
            _dependency.Trigger();
            if (PropertyChanged != null)
            {
                TaskManager.QueueNotification(this, nameof(Value), (e) =>
                {
                    PropertyChanged?.Invoke(this, e);
                });
            }
        }

        /// <summary>
        /// Disposes the internal effect used for recomputation.
        /// </summary>
        public void Dispose()
        {
            _effect.Dispose();
        }

        /// <summary>
        /// Tracks the dependency and recursively tracks nested reactive values.
        /// </summary>
        public void TrackDeep()
        {
            _dependency.Track();
            var value = Value;
            if (value != null && value is IReactive reactiveValue)
            {
                reactiveValue.TrackDeep();
            }
        }
    }
}
