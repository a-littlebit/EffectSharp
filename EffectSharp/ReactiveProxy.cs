using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;

namespace EffectSharp
{
    /// <summary>
    /// Dynamic reactive proxy for interfaces and classes, providing property tracking,
    /// change notifications, deep tracking support, and optional delegation to a target instance.
    /// </summary>
    /// <typeparam name="T">The proxied type.</typeparam>
    public class ReactiveProxy<T> : DispatchProxy, IReactive, INotifyPropertyChanging, INotifyPropertyChanged
        where T : class
    {
        private static readonly PropertyInfo[] _propertyCache;
        private static readonly Dictionary<string, int> _propertyOffset;
        private static readonly ReactivePropertyAttribute[] _reactivePropertyCache;

        static ReactiveProxy()
        {
            var type = typeof(T);
            _propertyCache = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            _propertyOffset = new Dictionary<string, int>(_propertyCache.Length);
            _reactivePropertyCache = new ReactivePropertyAttribute[_propertyCache.Length];
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                var prop = _propertyCache[i];
                _propertyOffset[prop.Name] = i;
                var attr = prop.GetCustomAttribute<ReactivePropertyAttribute>() ?? new ReactivePropertyAttribute();
                if (attr.Default == null && prop.PropertyType.IsValueType)
                    attr.Default = Activator.CreateInstance(prop.PropertyType);
                attr.EqualsFunc = GetEqualsFunc(attr.EqualityComparer, attr.EqualityComparerConstructorArgs, prop.PropertyType);
                _reactivePropertyCache[i] = attr;
            }
        }

        private static Func<object?, object?, bool> GetEqualsFunc(Type? comparerType, object?[]? constructorArgs, Type propertyType)
        {
            if (comparerType == null)
            {
                var defaultComparerType = typeof(EqualityComparer<>).MakeGenericType(propertyType);
                var defaultComparerInstance = (IEqualityComparer)defaultComparerType
                    .GetProperty(nameof(EqualityComparer<object>.Default)).GetValue(null);
                return defaultComparerInstance.Equals;
            }

            var instance = Activator.CreateInstance(comparerType, constructorArgs);
            if (instance is IEqualityComparer equalityComparer)
            {
                return equalityComparer.Equals;
            }
            else
            {
                var interfaceType = typeof(IEqualityComparer<>).MakeGenericType(propertyType);
                if (!interfaceType.IsAssignableFrom(comparerType))
                    throw new ArgumentException($"Type '{comparerType.FullName}' does not implement IEqualityComparer<{propertyType.Name}>.");
                var equalsMethod = interfaceType.GetMethod(nameof(IEqualityComparer<object>.Equals));
                return (a, b) => (bool)equalsMethod.Invoke(instance, new[] { a, b });
            }
        }

        private interface IStorage
        {
            T? Target { get; }
            object? GetValue(int offset);
            void SetValue(int offset, object? value);
        }

        private class ValueStorage : IStorage
        {
            private static readonly ThreadLocal<bool> _isInitializing = new();
            private readonly object?[] _values;

            public ValueStorage()
            {
                if (_isInitializing.Value)
                    throw new InvalidOperationException($"Recursive construction of ReactiveProxy<{typeof(T).Name}> detected. Check the deep properties to avoid infinite recursion.");

                _values = new object[_propertyCache.Length];
                _isInitializing.Value = true;

                try
                {
                    for (int i = 0; i < _propertyCache.Length; i++)
                    {
                        var prop = _propertyCache[i];
                        var reactiveAttr = _reactivePropertyCache[i];

                        object? initialValue;
                        if (reactiveAttr.Deep && prop.PropertyType.IsInterface)
                            initialValue = Reactive.Create(prop.PropertyType);
                        else
                            initialValue = reactiveAttr.Default;

                        _values[i] = initialValue;
                    }
                }
                finally
                {
                    _isInitializing.Value = false;
                }
            }

            public T? Target => null;
            public object? GetValue(int offset) => Volatile.Read(ref _values[offset]);
            public void SetValue(int offset, object? value) => Interlocked.Exchange(ref _values[offset], value);
        }

        private class TargetStorage : IStorage
        {
            private T _target;

            public TargetStorage(T target)
            {
                if (target == null)
                    throw new ArgumentNullException(nameof(target));
                _target = target;
            }

            public T Target => _target;
            public object? GetValue(int offset) => _propertyCache[offset].GetValue(_target);
            public void SetValue(int offset, object? value) => _propertyCache[offset].SetValue(_target, value);
        }

        private Dependency[] _dependencies;
        private IStorage? _storage;

        private IStorage? Storage => _storage ?? Volatile.Read(ref _storage);

        /// <summary>
        /// Raised before a reactive property value changes.
        /// </summary>
        public event PropertyChangingEventHandler? PropertyChanging;
        /// <summary>
        /// Raised after a reactive property value has changed.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// The underlying target instance if initialized via <see cref="InitializeForTarget(T)"/>; otherwise null.
        /// </summary>
        public T? Target => Storage?.Target;

        public ReactiveProxy()
        {
            _dependencies = new Dependency[_propertyCache.Length];
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                var reactiveAttr = _reactivePropertyCache[i];
                if (reactiveAttr.Reactive)
                {
                    _dependencies[i] = new Dependency();
                }
            }
        }

        /// <summary>
        /// Initializes the proxy to store values internally without a backing target.
        /// Deep properties marked with <see cref="ReactivePropertyAttribute.Deep"/> will be created as reactive proxies.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the proxy is already initialized.</exception>
        public void InitializeForValues()
        {
            if (Storage != null)
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is already initialized.");
            var storage = new ValueStorage();
            if (Interlocked.CompareExchange(ref _storage, storage, null) != null)
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is already initialized.");
        }

        /// <summary>
        /// Initializes the proxy to delegate property accessors to the specified target instance.
        /// Properties participate in dependency tracking by default unless explicitly marked with
        /// <see cref="ReactivePropertyAttribute.Reactive"/> = <c>false</c> on the interface property.
        /// </summary>
        /// <param name="target">The target instance to proxy.</param>
        /// <exception cref="InvalidOperationException">Thrown if the proxy is already initialized.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="target"/> is null.</exception>
        public void InitializeForTarget(T target)
        {
            if (Storage != null)
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is already initialized.");
            var storage = new TargetStorage(target);
            if (Interlocked.CompareExchange(ref _storage, storage, null) != null)
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is already initialized.");
        }

        private IStorage GetStorageOrThrow() =>
            Storage ?? throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is not initialized. Call InitializeForValues() or InitializeForTarget(T) before use.");

        /// <summary>
        /// Tracks all reactive dependencies for each property and recursively tracks nested reactive values.
        /// </summary>
        public void TrackDeep()
        {
            var storage = GetStorageOrThrow();
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                _dependencies[i]?.Track();
                var value = storage.GetValue(i);
                if (value != null && value is IReactive reactiveValue)
                {
                    reactiveValue.TrackDeep();
                }
            }
        }

        /// <summary>
        /// Gets the value of the specified property, participating in dependency tracking if reactive.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="targetMethod">Optional target method used when delegating to target for non-tracked access.</param>
        /// <returns>The property value.</returns>
        /// <exception cref="ArgumentException">Thrown if the property is not found.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the proxy is not initialized.</exception>
        public object? GetPropertyValue(string propertyName, MethodInfo? targetMethod = null)
        {
            var storage = GetStorageOrThrow();
            if (!_propertyOffset.TryGetValue(propertyName, out var offset))
            {
                var target = storage.Target;
                if (target != null && targetMethod != null)
                {
                    return targetMethod.Invoke(target, null);
                }
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }
            _dependencies[offset]?.Track();
            return storage.GetValue(offset);
        }

        /// <summary>
        /// Sets the value of the specified property and triggers dependency notifications if reactive.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="targetMethod">Optional target method used when delegating to target for non-tracked access.</param>
        /// <exception cref="ArgumentException">Thrown if the property is not found.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the proxy is not initialized.</exception>
        public void SetPropertyValue(string propertyName, object? value, MethodInfo? targetMethod = null)
        {
            var storage = GetStorageOrThrow();
            if (!_propertyOffset.TryGetValue(propertyName, out var offset))
            {
                var target = storage.Target;
                if (target != null && targetMethod != null)
                {
                    targetMethod.Invoke(target, new object?[] { value });
                    return;
                }
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }

            var currentValue = storage.GetValue(offset);
            if (_reactivePropertyCache[offset].EqualsFunc!(currentValue, value))
            {
                return;
            }

            Dependency dependency = _dependencies[offset];

            if (dependency != null)
                PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

            storage.SetValue(offset, value);

            if (dependency != null)
            {
                dependency.Trigger();
                if (PropertyChanged != null)
                {
                    TaskManager.QueueNotification(this, propertyName, (args) =>
                    {
                        PropertyChanged?.Invoke(this, args);
                    });
                }
            }
        }

        /// <summary>
        /// Intercepts method calls and routes property getters/setters to reactive handlers,
        /// or delegates to the target instance for regular methods.
        /// </summary>
        /// <param name="targetMethod">Method invoked on the proxy.</param>
        /// <param name="args">Arguments for the method.</param>
        /// <returns>Return value from the invocation.</returns>
        /// <exception cref="NotImplementedException">Thrown if the method is not implemented in the proxy.</exception>
        protected override object? Invoke(MethodInfo targetMethod, object[] args)
        {
            if (!targetMethod.IsSpecialName)
            {
                if (targetMethod.DeclaringType == typeof(IReactive) && targetMethod.Name == nameof(IReactive.TrackDeep))
                {
                    TrackDeep();
                    return null;
                }
                var storage = GetStorageOrThrow();
                var target = storage.Target;
                if (target != null)
                {
                    return targetMethod.Invoke(target, args);
                }
                throw new NotImplementedException($"Method '{targetMethod.Name}' is not implemented in ReactiveProxy.");
            }

            switch (targetMethod.Name)
            {
                case var name when name.StartsWith("get_"):
                    var propertyName = name.Substring(4);
                    return GetPropertyValue(propertyName, targetMethod);

                case var name when name.StartsWith("set_"):
                    propertyName = name.Substring(4);
                    SetPropertyValue(propertyName, args[0], targetMethod);
                    return null;

                case "add_PropertyChanged":
                    PropertyChanged += (PropertyChangedEventHandler)args[0];
                    return null;

                case "remove_PropertyChanged":
                    PropertyChanged -= (PropertyChangedEventHandler)args[0];
                    return null;

                case "add_PropertyChanging":
                    PropertyChanging += (PropertyChangingEventHandler)args[0];
                    return null;

                case "remove_PropertyChanging":
                    PropertyChanging -= (PropertyChangingEventHandler)args[0];
                    return null;

                default:
                    throw new NotImplementedException($"Method '{targetMethod.Name}' is not implemented in ReactiveProxy.");
            }
        }
    }
}
