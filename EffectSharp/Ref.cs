using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp
{
    public class Ref<T> : IRef<T>, IReactive, INotifyPropertyChanging, INotifyPropertyChanged
    {
        public event PropertyChangingEventHandler? PropertyChanging;
        public event PropertyChangedEventHandler? PropertyChanged;

        private Dependency _dependency = new();

        private T _value;
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
                    _value = value;
                    if (PropertyChanged != null)
                    {
                        DependencyTracker.EnqueueNotify(this, nameof(Value), (e) =>
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

        public Dependency? GetDependency(string propertyName)
        {
            if (propertyName == nameof(Value))
            {
                return _dependency;
            }
            return null;
        }
    }
}
