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
        private volatile object _value;
        private volatile bool _isDirty = true;
        private readonly Func<T> _getter;
        private readonly Action _setter;
        private readonly Dependency _dependency = new Dependency();
        private readonly Effect _effect;

        private bool _isDeep = false;

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public Computed(Func<T> getter, Action setter = null)
        {
            _getter = getter;
            _setter = setter;
            _effect = new Effect(() =>
            {
                if (!_isDirty) return;
                var value = _getter();

                if (_isDeep)
                {
                    var reactiveValue = Reactive.TryCreate(value);
                    if (value is IReactive r)
                    {
                        r.SetDeep();
                    }
                    _value = reactiveValue;
                }
                else
                {
                    _value = value;
                }

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

                return (T)_value;
            }
            set
            {
                if (_setter == null)
                    throw new InvalidOperationException("This computed property is read-only.");
                _setter();
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

        public bool SetDeep()
        {
            if (_isDeep) return false;
            _isDeep = true;
            if (_value == null) return true;
            if (_value is IReactive reactiveValue)
            {
                reactiveValue.SetDeep();
            }
            else
            {
                var deepValue = Reactive.TryCreate(_value);
                if (deepValue is IReactive deepReactiveValue)
                {
                    deepReactiveValue.SetDeep();
                    _value = deepValue;
                }
            }
            return true;
        }

        public void TrackDeep()
        {
            _dependency.Track();
            if (_value == null) return;
            if (_value is IReactive reactiveValue)
            {
                reactiveValue.TrackDeep();
            }
        }
    }
}
