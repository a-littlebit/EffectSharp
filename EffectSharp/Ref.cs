using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    public abstract class RefBase<T> : IRef<T>, IReadOnlyRef<T>, IReactive, INotifyPropertyChanging, INotifyPropertyChanged
    {
        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        private Dependency _dependency = new Dependency();
        private IEqualityComparer<T> _equalityComparer;

        protected abstract T Read();
        protected abstract void Write(T value);
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

        protected RefBase(IEqualityComparer<T> equalityComparer = null)
        {
            _equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
        }

        public T Value
        {
            get
            {
                _dependency.Track();
                return Read();
            }
            set
            {
                if (_equalityComparer.Equals(Read(), value))
                {
                    return;
                }

                PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(Value)));

                Write(value);

                if (PropertyChanged != null)
                {
                    TaskManager.EnqueueNotification(this, nameof(Value), (e) =>
                    {
                        PropertyChanged?.Invoke(this, e);
                    });
                }
                _dependency.Trigger();
            }
        }

        public virtual bool SetDeep()
        {
            return false;
        }

        public virtual void TrackDeep()
        {
            _dependency.Track(); 
        }
    }

    /// <summary>
    /// A reactive reference that holds a value of type T and notifies subscribers on changes.
    /// </summary>
    /// <typeparam name="T">The type of the value held by the reference. </typeparam>
    public class Ref<T> : RefBase<T>
    {
        private T _value;

        private bool _isDeep = false;

        public Ref(T value, IEqualityComparer<T> equalityComparer = null)
            : base(equalityComparer)
        {
            _value = value;
        }

        protected override T Read()
        {
            return _value;
        }

        protected override void Write(T value)
        {
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
        }

        public override bool SetDeep()
        {
            if (_isDeep) return false;
            _isDeep = true;

            var value = _value;
            if (value == null) return true;
            if (value is IReactive reactiveValue)
            {
                reactiveValue.SetDeep();
            }
            else
            {
                var deepValue = Reactive.TryCreate(value);
                if (deepValue is IReactive deepReactiveValue)
                {
                    deepReactiveValue.SetDeep();
                    _value = deepValue;
                }
            }
            return true;
        }

        public override void TrackDeep()
        {
            Track();

            var value = _value;
            if (value != null && value is IReactive reactiveValue)
            {
                reactiveValue.TrackDeep();
            }
        }
    }

    public class AtomicObjectRef<T> : RefBase<T>
        where T : class
    {
        private T _value;
        public AtomicObjectRef(T value, IEqualityComparer<T> equalityComparer)
            : base(equalityComparer)
        {
            _value = value;
        }
        protected override T Read() => Volatile.Read(ref _value);
        protected override void Write(T value) => Volatile.Write(ref _value, value);

        public override void TrackDeep()
        {
            Track();
            var value = Volatile.Read(ref _value);
            if (value != null && value is IReactive reactiveValue)
            {
                reactiveValue.TrackDeep();
            }
        }

        public T CompareExchange(T newValue, T comparand)
        {
            var value = Interlocked.CompareExchange(ref _value, newValue, comparand);
            if (ReferenceEquals(value, comparand))
            {
                AfterChange();
            }
            return value;
        }
        public T Exchange(T newValue)
        {
            var value = Interlocked.Exchange(ref _value, newValue);
            if (!ReferenceEquals(value, newValue))
            {
                AfterChange();
            }
            return value;
        }
    }

    public class AtomicIntRef : RefBase<int>
    {
        private int _value;

        public AtomicIntRef(int value = 0)
        {
            _value = value;
        }

        protected override int Read() => Volatile.Read(ref _value);
        protected override void Write(int value) => Volatile.Write(ref _value, value);

        public int Increment()
        {
            BeforeChange();
            var value = Interlocked.Increment(ref _value);
            AfterChange();
            return value;
        }

        public int Decrement()
        {
            BeforeChange();
            var value = Interlocked.Decrement(ref _value);
            AfterChange();
            return value;
        }

        public int Add(int delta)
        {
            BeforeChange();
            var value = Interlocked.Add(ref _value, delta);
            AfterChange();
            return value;
        }

        public int CompareExchange(int newValue, int comparand)
        {
            var value = Interlocked.CompareExchange(ref _value, newValue, comparand);
            if (value == comparand)
            {
                AfterChange();
            }
            return value;
        }

        public int Exchange(int newValue)
        {
            var value = Interlocked.Exchange(ref _value, newValue);
            if (value != newValue)
            {
                AfterChange();
            }
            return value;
        }
    }

    public class AtomicLongRef : RefBase<long>
    {
        private long _value;
        public AtomicLongRef(long value = 0)
        {
            _value = value;
        }
        protected override long Read() => Volatile.Read(ref _value);
        protected override void Write(long value) => Volatile.Write(ref _value, value);
        public long Increment()
        {
            BeforeChange();
            var value = Interlocked.Increment(ref _value);
            AfterChange();
            return value;
        }
        public long Decrement()
        {
            BeforeChange();
            var value = Interlocked.Decrement(ref _value);
            AfterChange();
            return value;
        }
        public long Add(long delta)
        {
            BeforeChange();
            var value = Interlocked.Add(ref _value, delta);
            AfterChange();
            return value;
        }
        public long CompareExchange(long newValue, long comparand)
        {
            var value = Interlocked.CompareExchange(ref _value, newValue, comparand);
            if (value == comparand)
            {
                AfterChange();
            }
            return value;
        }
        public long Exchange(long newValue)
        {
            var value = Interlocked.Exchange(ref _value, newValue);
            if (value != newValue)
            {
                AfterChange();
            }
            return value;
        }
    }

    public class AtomicFloatRef : RefBase<float>
    {
        private float _value;
        public AtomicFloatRef(float value = 0)
        {
            _value = value;
        }

        protected override float Read() => Volatile.Read(ref _value);
        protected override void Write(float value) => Volatile.Write(ref _value, value);

        public float Exchange(float newValue)
        {
            var value = Interlocked.Exchange(ref _value, newValue);
            if (value != newValue)
            {
                AfterChange();
            }
            return value;
        }

        public float CompareExchange(float newValue, float comparand)
        {
            var value = Interlocked.CompareExchange(ref _value, newValue, comparand);
            if (value == comparand)
            {
                AfterChange();
            }
            return value;
        }
    }

    public class AtomicDoubleRef : RefBase<double>
    {
        private double _value;
        public AtomicDoubleRef(double value = 0)
        {
            _value = value;
        }
        protected override double Read() => Volatile.Read(ref _value);
        protected override void Write(double value) => Volatile.Write(ref _value, value);
        public double Exchange(double newValue)
        {
            var value = Interlocked.Exchange(ref _value, newValue);
            if (value != newValue)
            {
                AfterChange();
            }
            return value;
        }
        public double CompareExchange(double newValue, double comparand)
        {
            var value = Interlocked.CompareExchange(ref _value, newValue, comparand);
            if (value == comparand)
            {
                AfterChange();
            }
            return value;
        }
    }
}
