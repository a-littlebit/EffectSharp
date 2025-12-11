using System;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;

namespace EffectSharp
{
    /// <summary>
    /// Interface for atomic read/write operations (supports value types without boxing and reference types)
    /// </summary>
    /// <typeparam name="T">Value type or reference type</typeparam>
    public interface IAtomic<T>
    {
        /// <summary>
        /// Atomic read/write property (replaces GetValue/SetValue methods)
        /// </summary>
        T Value { get; set; }

        /// <summary>
        /// Atomic compare and exchange (CAS) operation
        /// </summary>
        /// <param name="newValue">New value to set if comparison succeeds</param>
        /// <param name="compareValue">Value to compare with current value</param>
        /// <returns>True if exchange succeeded, false otherwise</returns>
        bool CompareExchange(T newValue, T compareValue);

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        T Exchange(T newValue);
    }

    #region Specialized Atomic Implementations for Primitive Value Types (No Boxing)
    /// <summary>
    /// Atomic implementation for int type (no boxing)
    /// </summary>
    public class AtomicInt : IAtomic<int>
    {
        private int _value;

        /// <summary>
        /// Initialize AtomicInt with default value (0)
        /// </summary>
        public AtomicInt() : this(0) { }

        /// <summary>
        /// Initialize AtomicInt with specific value
        /// </summary>
        /// <param name="initialValue">Initial int value</param>
        public AtomicInt(int initialValue) => _value = initialValue;

        /// <summary>
        /// Atomic read/write int value (no boxing)
        /// </summary>
        public int Value
        {
            get => Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// Atomic compare and exchange for int
        /// </summary>
        /// <param name="newValue">New int value</param>
        /// <param name="compareValue">Comparison int value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(int newValue, int compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public int Exchange(int newValue)
        {
            return Interlocked.Exchange(ref _value, newValue);
        }

        /// <summary>
        /// Atomically increments the value and returns the new value.
        /// </summary>
        public int Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        /// <summary>
        /// Atomically decrements the value and returns the new value.
        /// </summary>
        public int Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }

        /// <summary>
        /// Atomically adds the specified delta and returns the new value.
        /// </summary>
        public int Add(int value)
        {
            return Interlocked.Add(ref _value, value);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicInt other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for long type (no boxing)
    /// </summary>
    public class AtomicLong : IAtomic<long>
    {
        private long _value;

        /// <summary>
        /// Initialize AtomicLong with default value (0)
        /// </summary>
        public AtomicLong() : this(0) { }

        /// <summary>
        /// Initialize AtomicLong with specific value
        /// </summary>
        /// <param name="initialValue">Initial long value</param>
        public AtomicLong(long initialValue) => _value = initialValue;

        /// <summary>
        /// Atomic read/write long value (no boxing)
        /// </summary>
        public long Value
        {
            get => Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// Atomic compare and exchange for long
        /// </summary>
        /// <param name="newValue">New long value</param>
        /// <param name="compareValue">Comparison long value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(long newValue, long compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public long Exchange(long newValue)
        {
            return Interlocked.Exchange(ref _value, newValue);
        }

        /// <summary>
        /// Atomically increments the value and returns the new value.
        /// </summary>
        public long Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        /// <summary>
        /// Atomically decrements the value and returns the new value.
        /// </summary>
        public long Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }

        /// <summary>
        /// Atomically adds the specified delta and returns the new value.
        /// </summary>
        public long Add(long value)
        {
            return Interlocked.Add(ref _value, value);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicLong other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for bool type (no boxing, based on int)
    /// </summary>
    public class AtomicBool : IAtomic<bool>
    {
        private int _value; // 0 = false, 1 = true

        /// <summary>
        /// Initialize AtomicBool with default value (false)
        /// </summary>
        public AtomicBool() : this(false) { }

        /// <summary>
        /// Initialize AtomicBool with specific value
        /// </summary>
        /// <param name="initialValue">Initial bool value</param>
        public AtomicBool(bool initialValue) => _value = initialValue ? 1 : 0;

        /// <summary>
        /// Atomic read/write bool value (no boxing)
        /// </summary>
        public bool Value
        {
            get => Interlocked.CompareExchange(ref _value, 0, 0) == 1;
            set => Interlocked.Exchange(ref _value, value ? 1 : 0);
        }

        /// <summary>
        /// Atomic compare and exchange for bool
        /// </summary>
        /// <param name="newValue">New bool value</param>
        /// <param name="compareValue">Comparison bool value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(bool newValue, bool compareValue)
        {
            int newInt = newValue ? 1 : 0;
            int compareInt = compareValue ? 1 : 0;
            return Interlocked.CompareExchange(ref _value, newInt, compareInt) == compareInt;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public bool Exchange(bool newValue)
        {
            return Interlocked.Exchange(ref _value, newValue ? 1 : 0) == 1;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicBool other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for float type (no boxing).
    /// </summary>
    public class AtomicFloat : IAtomic<float>
    {
        private float _value;

        /// <summary>
        /// Initialize AtomicFloat with default value (0.0f)
        /// </summary>
        public AtomicFloat() : this(0.0f) { }

        /// <summary>
        /// Initialize AtomicFloat with specific value
        /// </summary>
        /// <param name="initialValue">Initial float value</param>
        public AtomicFloat(float initialValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Atomic read/write float value (no boxing)
        /// </summary>
        public float Value
        {
            get
            {
                return Interlocked.CompareExchange(ref _value, 0.0f, 0.0f);
            }
            set
            {
                Interlocked.Exchange(ref _value, value);
            }
        }

        /// <summary>
        /// Atomic compare and exchange for float
        /// </summary>
        /// <param name="newValue">New float value</param>
        /// <param name="compareValue">Comparison float value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(float newValue, float compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public float Exchange(float newValue)
        {
            return Interlocked.Exchange(ref _value, newValue);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicFloat other) return Value.Equals(other.Value);
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for double type (no boxing).
    /// </summary>
    public class AtomicDouble : IAtomic<double>
    {
        private double _value;

        /// <summary>
        /// Initialize AtomicDouble with default value (0.0)
        /// </summary>
        public AtomicDouble() : this(0.0) { }

        /// <summary>
        /// Initialize AtomicDouble with specific value
        /// </summary>
        /// <param name="initialValue">Initial double value</param>
        public AtomicDouble(double initialValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Atomic read/write double value (no boxing)
        /// </summary>
        public double Value
        {
            get
            {
                return Interlocked.CompareExchange(ref _value, 0.0, 0.0);
            }
            set
            {
                Interlocked.Exchange(ref _value, value);
            }
        }

        /// <summary>
        /// Atomic compare and exchange for double
        /// </summary>
        /// <param name="newValue">New double value</param>
        /// <param name="compareValue">Comparison double value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(double newValue, double compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public double Exchange(double newValue)
        {
            return Interlocked.Exchange(ref _value, newValue);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicDouble other) return Value.Equals(other.Value);
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for byte type (no boxing, mapped to int)
    /// </summary>
    public class AtomicByte : IAtomic<byte>
    {
        private int _value;

        /// <summary>
        /// Initialize AtomicByte with default value (0)
        /// </summary>
        public AtomicByte() : this(0) { }

        /// <summary>
        /// Initialize AtomicByte with specific value
        /// </summary>
        /// <param name="initialValue">Initial byte value</param>
        public AtomicByte(byte initialValue) => _value = initialValue;

        /// <summary>
        /// Atomic read/write byte value (no boxing)
        /// </summary>
        public byte Value
        {
            get => (byte)Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// Atomic compare and exchange for byte
        /// </summary>
        /// <param name="newValue">New byte value</param>
        /// <param name="compareValue">Comparison byte value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(byte newValue, byte compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public byte Exchange(byte newValue)
        {
            return (byte)Interlocked.Exchange(ref _value, newValue);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicByte other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for short type (no boxing, mapped to int)
    /// </summary>
    public class AtomicShort : IAtomic<short>
    {
        private int _value;

        /// <summary>
        /// Initialize AtomicShort with default value (0)
        /// </summary>
        public AtomicShort() : this(0) { }

        /// <summary>
        /// Initialize AtomicShort with specific value
        /// </summary>
        /// <param name="initialValue">Initial short value</param>
        public AtomicShort(short initialValue) => _value = initialValue;

        /// <summary>
        /// Atomic read/write short value (no boxing)
        /// </summary>
        public short Value
        {
            get => (short)Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// Atomic compare and exchange for short
        /// </summary>
        /// <param name="newValue">New short value</param>
        /// <param name="compareValue">Comparison short value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(short newValue, short compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public short Exchange(short newValue)
        {
            return (short)Interlocked.Exchange(ref _value, newValue);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicShort other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for uint type (no boxing, mapped to int)
    /// </summary>
    public class AtomicUInt : IAtomic<uint>
    {
        private int _value;

        /// <summary>
        /// Initialize AtomicUInt with default value (0)
        /// </summary>
        public AtomicUInt() : this(0) { }

        /// <summary>
        /// Initialize AtomicUInt with specific value
        /// </summary>
        /// <param name="initialValue">Initial uint value</param>
        public AtomicUInt(uint initialValue) => _value = unchecked((int)initialValue);

        /// <summary>
        /// Atomic read/write uint value (no boxing)
        /// </summary>
        public uint Value
        {
            get => unchecked((uint)Interlocked.CompareExchange(ref _value, 0, 0));
            set => Interlocked.Exchange(ref _value, unchecked((int)value));
        }

        /// <summary>
        /// Atomic compare and exchange for uint
        /// </summary>
        /// <param name="newValue">New uint value</param>
        /// <param name="compareValue">Comparison uint value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(uint newValue, uint compareValue)
        {
            int newInt = unchecked((int)newValue);
            int compareInt = unchecked((int)compareValue);
            return Interlocked.CompareExchange(ref _value, newInt, compareInt) == compareInt;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public uint Exchange(uint newValue)
        {
            return unchecked((uint)Interlocked.Exchange(ref _value, unchecked((int)newValue)));
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicUInt other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for ulong type (no boxing, mapped to long)
    /// </summary>
    public class AtomicULong : IAtomic<ulong>
    {
        private long _value;

        /// <summary>
        /// Initialize AtomicULong with default value (0)
        /// </summary>
        public AtomicULong() : this(0) { }

        /// <summary>
        /// Initialize AtomicULong with specific value
        /// </summary>
        /// <param name="initialValue">Initial ulong value</param>
        public AtomicULong(ulong initialValue) => _value = unchecked((long)initialValue);

        /// <summary>
        /// Atomic read/write ulong value (no boxing)
        /// </summary>
        public ulong Value
        {
            get => unchecked((ulong)Interlocked.CompareExchange(ref _value, 0, 0));
            set => Interlocked.Exchange(ref _value, unchecked((long)value));
        }

        /// <summary>
        /// Atomic compare and exchange for ulong
        /// </summary>
        /// <param name="newValue">New ulong value</param>
        /// <param name="compareValue">Comparison ulong value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(ulong newValue, ulong compareValue)
        {
            long newLong = unchecked((long)newValue);
            long compareLong = unchecked((long)compareValue);
            return Interlocked.CompareExchange(ref _value, newLong, compareLong) == compareLong;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public ulong Exchange(ulong newValue)
        {
            return unchecked((ulong)Interlocked.Exchange(ref _value, unchecked((long)newValue)));
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicULong other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for char type (no boxing, mapped to int)
    /// </summary>
    public class AtomicChar : IAtomic<char>
    {
        private int _value;

        /// <summary>
        /// Initialize AtomicChar with default value ('\0')
        /// </summary>
        public AtomicChar() : this('\0') { }

        /// <summary>
        /// Initialize AtomicChar with specific value
        /// </summary>
        /// <param name="initialValue">Initial char value</param>
        public AtomicChar(char initialValue) => _value = initialValue;

        /// <summary>
        /// Atomic read/write char value (no boxing)
        /// </summary>
        public char Value
        {
            get => (char)Interlocked.CompareExchange(ref _value, 0, 0);
            set => Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// Atomic compare and exchange for char
        /// </summary>
        /// <param name="newValue">New char value</param>
        /// <param name="compareValue">Comparison char value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(char newValue, char compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public char Exchange(char newValue)
        {
            return (char)Interlocked.Exchange(ref _value, newValue);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicChar other) return Value == other.Value;
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for DateTime type (no boxing, based on Ticks)
    /// </summary>
    public class AtomicDateTime : IAtomic<DateTime>
    {
        private long _ticks;

        /// <summary>
        /// Initialize AtomicDateTime with default value (DateTime.MinValue)
        /// </summary>
        public AtomicDateTime() : this(DateTime.MinValue) { }

        /// <summary>
        /// Initialize AtomicDateTime with specific value
        /// </summary>
        /// <param name="initialValue">Initial DateTime value</param>
        public AtomicDateTime(DateTime initialValue) => _ticks = initialValue.Ticks;

        /// <summary>
        /// Atomic read/write DateTime value (no boxing)
        /// </summary>
        public DateTime Value
        {
            get => new DateTime(Interlocked.CompareExchange(ref _ticks, 0, 0));
            set => Interlocked.Exchange(ref _ticks, value.Ticks);
        }

        /// <summary>
        /// Atomic compare and exchange for DateTime
        /// </summary>
        /// <param name="newValue">New DateTime value</param>
        /// <param name="compareValue">Comparison DateTime value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(DateTime newValue, DateTime compareValue)
        {
            return Interlocked.CompareExchange(ref _ticks, newValue.Ticks, compareValue.Ticks) == compareValue.Ticks;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public DateTime Exchange(DateTime newValue)
        {
            return new DateTime(Interlocked.Exchange(ref _ticks, newValue.Ticks));
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicDateTime other) return Value.Equals(other.Value);
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }

    /// <summary>
    /// Atomic implementation for TimeSpan type (no boxing, based on Ticks)
    /// </summary>
    public class AtomicTimeSpan : IAtomic<TimeSpan>
    {
        private long _ticks;

        /// <summary>
        /// Initialize AtomicTimeSpan with default value (TimeSpan.Zero)
        /// </summary>
        public AtomicTimeSpan() : this(TimeSpan.Zero) { }

        /// <summary>
        /// Initialize AtomicTimeSpan with specific value
        /// </summary>
        /// <param name="initialValue">Initial TimeSpan value</param>
        public AtomicTimeSpan(TimeSpan initialValue) => _ticks = initialValue.Ticks;

        /// <summary>
        /// Atomic read/write TimeSpan value (no boxing)
        /// </summary>
        public TimeSpan Value
        {
            get => new TimeSpan(Interlocked.CompareExchange(ref _ticks, 0, 0));
            set => Interlocked.Exchange(ref _ticks, value.Ticks);
        }

        /// <summary>
        /// Atomic compare and exchange for TimeSpan
        /// </summary>
        /// <param name="newValue">New TimeSpan value</param>
        /// <param name="compareValue">Comparison TimeSpan value</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(TimeSpan newValue, TimeSpan compareValue)
        {
            return Interlocked.CompareExchange(ref _ticks, newValue.Ticks, compareValue.Ticks) == compareValue.Ticks;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <returns>Previous value before the exchange</returns>
        public TimeSpan Exchange(TimeSpan newValue)
        {
            return new TimeSpan(Interlocked.Exchange(ref _ticks, newValue.Ticks));
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (obj is AtomicTimeSpan other) return Value.Equals(other.Value);
            return false;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
    }
    #endregion

    #region Generic Boxed Implementation (For Complex Value Types/Reference Types)
    /// <summary>
    /// Generic atomic implementation (boxed for complex value types, no boxing for reference types)
    /// </summary>
    /// <typeparam name="T">Complex value type or reference type</typeparam>
    public class AtomicReference<T> : IAtomic<T>
    {
        private object _value;

        /// <summary>
        /// Initialize AtomicBoxed with default value
        /// </summary>
        public AtomicReference() : this(default) { }

        /// <summary>
        /// Initialize AtomicBoxed with specific value
        /// </summary>
        /// <param name="initialValue">Initial value (boxed if complex value type)</param>
        public AtomicReference(T initialValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Atomic read/write value (boxed for complex value types, no boxing for reference types)
        /// </summary>
        public T Value
        {
            get
            {
                return (T)Interlocked.CompareExchange(ref _value, null, null);
            }
            set => Interlocked.Exchange(ref _value, value);
        }

        /// <summary>
        /// Atomic compare and exchange for boxed values
        /// </summary>
        /// <param name="newValue">New value to set</param>
        /// <param name="compareValue">Value to compare with</param>
        /// <returns>True if exchange succeeded</returns>
        public bool CompareExchange(T newValue, T compareValue)
        {
            return Interlocked.CompareExchange(ref _value, newValue, compareValue) == (object)compareValue;
        }

        /// <summary>
        /// Atomically exchange the current value with a new value.
        /// </summary>
        public T Exchange(T newValue)
        {
            return (T)Interlocked.Exchange(ref _value, newValue);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (!(obj is AtomicReference<T> other)) return false;

            return ReferenceEquals(_value, other._value);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(_value);
        }
    }
    #endregion

    #region Atomic Factory (Auto Select Optimal Implementation)
    /// <summary>
    /// Factory class to create atomic instances (auto select specialized implementation for primitive types)
    /// </summary>
    public static class AtomicFactory<T>
    {
        private static readonly Func<T, IAtomic<T>> _creator;

        static AtomicFactory()
        {
            switch (typeof(T))
            {
                case Type t when t == typeof(int):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicInt((int)(object)initialValue);
                    break;
                case Type t when t == typeof(long):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicLong((long)(object)initialValue);
                    break;
                case Type t when t == typeof(bool):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicBool((bool)(object)initialValue);
                    break;
                case Type t when t == typeof(float):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicFloat((float)(object)initialValue);
                    break;
                case Type t when t == typeof(double):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicDouble((double)(object)initialValue);
                    break;
                case Type t when t == typeof(byte):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicByte((byte)(object)initialValue);
                    break;
                case Type t when t == typeof(short):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicShort((short)(object)initialValue);
                    break;
                case Type t when t == typeof(uint):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicUInt((uint)(object)initialValue);
                    break;
                case Type t when t == typeof(ulong):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicULong((ulong)(object)initialValue);
                    break;
                case Type t when t == typeof(char):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicChar((char)(object)initialValue);
                    break;
                case Type t when t == typeof(DateTime):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicDateTime((DateTime)(object)initialValue);
                    break;
                case Type t when t == typeof(TimeSpan):
                    _creator = (initialValue) => (IAtomic<T>)new AtomicTimeSpan((TimeSpan)(object)initialValue);
                    break;
                default:
                    _creator = (initialValue) => new AtomicReference<T>(initialValue);
                    break;
            }
        }

        /// <summary>
        /// Create <see cref="IAtomic{T}"/> instance with specified initial value.
        /// </summary>
        /// <param name="initialValue">Initial value (default: default(T))</param>
        /// <returns>Optimal <see cref="IAtomic{T}"/> implementation for <typeparamref name="T"/>.</returns>
        public static IAtomic<T> Create(T initialValue = default)
        {
            return _creator(initialValue);
        }
    }
    #endregion
}