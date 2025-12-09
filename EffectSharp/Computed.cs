using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Represents a computed reactive value that automatically updates when its dependencies change.
    /// </summary>
    /// <typeparam name="T">The type of the computed value. </typeparam>
    public class Computed<T> : INotifyPropertyChanging, INotifyPropertyChanged, IReactive, IRef<T>, IDisposable
    {
        private volatile IAtomic<T> _value;
        private volatile bool _isDirty = true;
        private readonly Action<T> _setter;
        private readonly Dependency _dependency = new Dependency();
        private readonly Effect _effect;

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public Computed(Func<T> getter, Action<T> setter = null)
        {
            _value = AtomicFactory<T>.Create();
            _setter = setter;
            _effect = new Effect(() =>
            {
                if (!_isDirty) return;

                _value.Value = getter();

                _isDirty = false;
            }, (_) => Invalidate(), true);
        }

        public T Value
        {
            get
            {
                _dependency.Track();

                if (_isDirty)
                {
                    lock (_effect)
                    {
                        if (_isDirty)
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
            _isDirty = true;
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
