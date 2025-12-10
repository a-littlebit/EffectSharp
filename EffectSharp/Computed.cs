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
        private volatile IAtomic<T> _value;
        private int _dirtyFlag = 2; // 0: clean, 1: computing, 2: dirty
        private readonly Action<T> _setter;
        private readonly Dependency _dependency = new Dependency();
        private readonly Effect _effect;

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public Computed(Func<T> getter, Action<T> setter = null)
        {
            _value = AtomicFactory<T>.Create();
            _setter = setter;
            _effect = new Effect(() => Validate(getter), (_) => Invalidate(), true);
        }

        private void Validate(Func<T> getter)
        {
            if (Interlocked.CompareExchange(ref _dirtyFlag, 1, 2) != 2)
            {
                return;
            }

            _value.Value = getter();

            Interlocked.CompareExchange(ref _dirtyFlag, 0, 1);
        }

        public T Value
        {
            get
            {
                _dependency.Track();

                if (Volatile.Read(ref _dirtyFlag) != 0)
                {
                    lock (_effect)
                    {
                        if (Volatile.Read(ref _dirtyFlag) != 0)
                        {
                            _effect.Execute();
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

        public void Invalidate()
        {
            Interlocked.Exchange(ref _dirtyFlag, 2);
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
            _dependency.Trigger();
            if (PropertyChanged != null)
            {
                TaskManager.EnqueueNotification(this, nameof(Value), (e) =>
                {
                    PropertyChanged?.Invoke(this, e);
                });
            }
        }

        public void Dispose()
        {
            _effect.Dispose();
        }

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
