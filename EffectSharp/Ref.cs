using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// A reactive reference that holds a value of type T and notifies subscribers on changes.
    /// </summary>
    /// <typeparam name="T">The type of the value held by the reference. </typeparam>
    public class Ref<T> : IRef<T>, IReactive, INotifyPropertyChanging, INotifyPropertyChanged
    {
        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        private Dependency _dependency = new Dependency();

        private T _value;
        private bool _isDeep = false;
        public T Value
        {
            get
            {
                _dependency.Track();
                return _value;
            }
            set
            {
                if (!EqualityComparer<T>.Default.Equals(_value, value))
                {
                    PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
                    
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

                    if (PropertyChanged != null)
                    {
                        TaskManager.EnqueueNotify(this, nameof(Value), (e) =>
                        {
                            PropertyChanged?.Invoke(this, e);
                        });
                    }
                    _dependency.Trigger();
                }
            }
        }

        public Ref(T initialValue)
        {
            _value = initialValue;
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
