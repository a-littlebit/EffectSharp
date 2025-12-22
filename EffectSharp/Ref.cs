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
        /// <summary>
        /// Raised before the value changes.
        /// </summary>
        public event PropertyChangingEventHandler? PropertyChanging;
        /// <summary>
        /// Raised after the value has changed.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Dependency _dependency = new();
        protected readonly IAtomic<T> _value;
        private readonly IEqualityComparer<T> _equalityComparer;

        /// <summary>
        /// Initializes a new reactive reference with an optional initial value and equality comparer.
        /// </summary>
        public Ref(T initialValue = default!, IEqualityComparer<T>? equalityComparer = null)
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
                TaskManager.QueueNotification(this, nameof(Value), (e) =>
                {
                    PropertyChanged?.Invoke(this, e);
                });
            }
        }

        /// <summary>
        /// Gets or sets the current value, participating in dependency tracking.
        /// </summary>
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

        /// <summary>
        /// Atomically sets the value to <paramref name="newValue"/> if the current value equals <paramref name="compared"/>.
        /// </summary>
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

        /// <summary>
        /// Atomically swaps the value with <paramref name="newValue"/> and returns the old value.
        /// </summary>
        public T Exchange(T newValue)
        {
            var oldValue = _value.Exchange(newValue);
            if (!_equalityComparer.Equals(oldValue, newValue))
            {
                AfterChange();
            }
            return oldValue;
        }

        /// <summary>
        /// Tracks the dependency and recursively tracks nested reactive values.
        /// </summary>
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

    /// <summary>
    /// Reactive atomic integer reference with convenience operations.
    /// </summary>
    public class AtomicIntRef : Ref<int>
    {
        /// <summary>
        /// Initializes a new atomic integer reference.
        /// </summary>
        public AtomicIntRef(int initialValue = 0) : base(initialValue, EqualityComparer<int>.Default) { }

        /// <summary>
        /// Atomically increments the value and returns the new value.
        /// </summary>
        public int Increment()
        {
            BeforeChange();
            var newValue = ((AtomicInt)_value).Increment();
            AfterChange();
            return newValue;
        }

        /// <summary>
        /// Atomically decrements the value and returns the new value.
        /// </summary>
        public int Decrement()
        {
            BeforeChange();
            var newValue = ((AtomicInt)_value).Decrement();
            AfterChange();
            return newValue;
        }

        /// <summary>
        /// Atomically adds <paramref name="delta"/> to the value and returns the new value.
        /// </summary>
        public int Add(int delta)
        {
            BeforeChange();
            var newValue = ((AtomicInt)_value).Add(delta);
            AfterChange();
            return newValue;
        }
    }

    /// <summary>
    /// Reactive atomic long reference with convenience operations.
    /// </summary>
    public class AtomicLongRef : Ref<long>
    {
        /// <summary>
        /// Initializes a new atomic long reference.
        /// </summary>
        public AtomicLongRef(long initialValue = 0) : base(initialValue, EqualityComparer<long>.Default) { }
        /// <summary>
        /// Atomically increments the value and returns the new value.
        /// </summary>
        public long Increment()
        {
            BeforeChange();
            var newValue = ((AtomicLong)_value).Increment();
            AfterChange();
            return newValue;
        }
        /// <summary>
        /// Atomically decrements the value and returns the new value.
        /// </summary>
        public long Decrement()
        {
            BeforeChange();
            var newValue = ((AtomicLong)_value).Decrement();
            AfterChange();
            return newValue;
        }
        /// <summary>
        /// Atomically adds <paramref name="delta"/> to the value and returns the new value.
        /// </summary>
        public long Add(long delta)
        {
            BeforeChange();
            var newValue = ((AtomicLong)_value).Add(delta);
            AfterChange();
            return newValue;
        }
    }
}
