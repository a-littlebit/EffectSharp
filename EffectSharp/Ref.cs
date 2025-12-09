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
    /// A reactive reference that holds a value of type T and notifies subscribers on changes.
    /// </summary>
    /// <typeparam name="T">The type of the value held by the reference. </typeparam>
    public class Ref<T> : IRef<T>, IReadOnlyRef<T>, IReactive, INotifyPropertyChanging, INotifyPropertyChanged
    {
        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Dependency _dependency = new Dependency();
        protected readonly IAtomic<T> _value;
        private readonly IEqualityComparer<T> _equalityComparer;

        public Ref(T initialValue = default, IEqualityComparer<T> equalityComparer = null)
        {
            _value = AtomicFactory<T>.Create(initialValue);
            _equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
        }

        protected void Track()
        {
            _dependency.Track();
        }

        protected void BeforeChange()
        {
            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));
        }

        protected void AfterChange()
        {
            _dependency.Trigger();
            if (PropertyChanged != null)
            {
                TaskManager.EnqueueNotification(this, nameof(Value), (e) =>
                {
                    PropertyChanged?.Invoke(this, e);
                });
            }
        }

        public T Value
        {
            get
            {
                _dependency.Track();
                return _value.Value;
            }
            set
            {
                if (_equalityComparer.Equals(_value.Value, value))
                {
                    return;
                }

                BeforeChange();

                _value.Value = value;

                AfterChange();
            }
        }

        public bool CompareExchange(T newValue, T compared)
        {
            if (_value.CompareExchange(newValue, compared))
            {
                if (!_equalityComparer.Equals(compared, newValue))
                {
                    AfterChange();
                }
                return true;
            }
            return false;
        }

        public T Exchange(T newValue)
        {
            var oldValue = _value.Exchange(newValue);
            if (!_equalityComparer.Equals(oldValue, newValue))
            {
                AfterChange();
            }
            return oldValue;
        }

        public void TrackDeep()
        {
            _dependency.Track();
            var value = _value.Value;
            if (value is IReactive reactive)
            {
                reactive.TrackDeep();
            }
        }
    }

    public class AtomicIntRef : Ref<int>
    {
        public AtomicIntRef(int initialValue = 0) : base(initialValue, EqualityComparer<int>.Default) { }

        public int Increment()
        {
            BeforeChange();
            var newValue = ((AtomicInt)_value).Increment();
            AfterChange();
            return newValue;
        }

        public int Decrement()
        {
            BeforeChange();
            var newValue = ((AtomicInt)_value).Decrement();
            AfterChange();
            return newValue;
        }

        public int Add(int delta)
        {
            BeforeChange();
            var newValue = ((AtomicInt)_value).Add(delta);
            AfterChange();
            return newValue;
        }
    }

    public class AtomicLongRef : Ref<long>
    {
        public AtomicLongRef(long initialValue = 0) : base(initialValue, EqualityComparer<long>.Default) { }
        public long Increment()
        {
            BeforeChange();
            var newValue = ((AtomicLong)_value).Increment();
            AfterChange();
            return newValue;
        }
        public long Decrement()
        {
            BeforeChange();
            var newValue = ((AtomicLong)_value).Decrement();
            AfterChange();
            return newValue;
        }
        public long Add(long delta)
        {
            BeforeChange();
            var newValue = ((AtomicLong)_value).Add(delta);
            AfterChange();
            return newValue;
        }
    }
}
