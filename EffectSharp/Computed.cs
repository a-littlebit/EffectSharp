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
        private T _value = default;
        private volatile bool _isDirty = true;
        private readonly Func<T> _getter;
        private readonly Action _setter;
        private readonly Dependency _dependency = new Dependency();
        private readonly Effect _effect;

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public Computed(Func<T> getter, Action setter = null)
        {
            _getter = getter;
            _setter = setter;
            _effect = new Effect(() =>
            {
                if (!_isDirty) return;
                _value = _getter();
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
                    _effect.Execute();
                }

                return _value;
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
                TaskManager.EnqueueNotify(this, nameof(Value), (e) =>
                {
                    PropertyChanged?.Invoke(this, e);
                });
            }
        }

        public void Dispose()
        {
            _effect.Dispose();
        }

        public Dependency GetDependency(string propertyName)
        {
            if (propertyName == nameof(Value))
                return _dependency;
            return null;
        }
    }
}
