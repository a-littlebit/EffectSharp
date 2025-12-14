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

        private static Func<object, object, bool> GetEqualsFunc(Type comparerType, object[] constructorArgs, Type propertyType)
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
                return (a, b) => (bool)equalsMethod.Invoke(instance, new object[] { a, b });
            }
        }

        private Dependency[] _dependencies;
        private object[] _values;
        private T _target;

        private static readonly ThreadLocal<bool> _isInitializing = new ThreadLocal<bool>();

        /// <summary>
        /// Raised before a reactive property value changes.
        /// </summary>
        public event PropertyChangingEventHandler PropertyChanging;
        /// <summary>
        /// Raised after a reactive property value has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The underlying target instance if initialized via <see cref="InitializeForTarget(T)"/>; otherwise null.
        /// </summary>
        public T Target => _target;

        public ReactiveProxy()
        {
        }

        /// <summary>
        /// Initializes the proxy to store values internally without a backing target.
        /// Deep properties marked with <see cref="ReactivePropertyAttribute.Deep"/> will be created as reactive proxies.
        /// </summary>
        public void InitializeForValues()
        {
            if (_isInitializing.Value)
                throw new InvalidOperationException($"Recursive construction of ReactiveProxy<{typeof(T).Name}> detected. Check the deep properties to avoid infinite recursion.");

            var deps = new Dependency[_propertyCache.Length];
            if (Interlocked.CompareExchange(ref _dependencies, deps, null) != null)
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is already initialized.");

            _values = new object[_propertyCache.Length];
            _isInitializing.Value = true;

            try
            {
                for (int i = 0; i < _propertyCache.Length; i++)
                {
                    var prop = _propertyCache[i];
                    var reactiveAttr = _reactivePropertyCache[i];

                    object initialValue;
                    if (reactiveAttr.Deep && prop.PropertyType.IsInterface)
                        initialValue = Reactive.Create(prop.PropertyType);
                    else
                        initialValue = reactiveAttr.Default;

                    _values[i] = initialValue;
                    if (reactiveAttr.Reactive)
                    {
                        _dependencies[i] = new Dependency();
                    }
                }
            }
            finally
            {
                _isInitializing.Value = false;
            }
        }

        /// <summary>
        /// Initializes the proxy to delegate property accessors to the specified target instance.
        /// Properties participate in dependency tracking by default unless explicitly marked with
        /// <see cref="ReactivePropertyAttribute.Reactive"/> = <c>false</c> on the interface property.
        /// </summary>
        /// <param name="target">The target instance to proxy.</param>
        public void InitializeForTarget(T target)
        {
            var deps = new Dependency[_propertyCache.Length];
            if (Interlocked.CompareExchange(ref _dependencies, deps, null) != null)
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is already initialized.");

            _target = target;
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                var reactiveAttr = _reactivePropertyCache[i];
                if (reactiveAttr.Reactive)
                {
                    _dependencies[i] = new Dependency();
                }
            }
        }

        private void ThrowIfNotInitialized()
        {
            if (_dependencies == null)
            {
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is not initialized. Call InitializeForTarget or InitializeForValues before using it.");
            }
        }

        /// <summary>
        /// Tracks all reactive dependencies for each property and recursively tracks nested reactive values.
        /// </summary>
        public void TrackDeep()
        {
            ThrowIfNotInitialized();
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                _dependencies[i]?.Track();
                object value;
                if (_target != null)
                    value = _propertyCache[i].GetValue(_target);
                else
                    value = Volatile.Read(ref _values[i]);
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
        public object GetPropertyValue(string propertyName, MethodInfo targetMethod = null)
        {
            ThrowIfNotInitialized();
            if (!_propertyOffset.TryGetValue(propertyName, out var offset))
            {
                if (_target != null && targetMethod != null)
                {
                    return targetMethod.Invoke(_target, null);
                }
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }
            _dependencies[offset]?.Track();
            if (_target != null)
            {
                return _propertyCache[offset].GetValue(_target);
            }
            else
            {
                return Volatile.Read(ref _values[offset]);
            }
        }

        /// <summary>
        /// Sets the value of the specified property and triggers dependency notifications if reactive.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="value">The value to set.</param>
        /// <param name="targetMethod">Optional target method used when delegating to target for non-tracked access.</param>
        public void SetPropertyValue(string propertyName, object value, MethodInfo targetMethod = null)
        {
            ThrowIfNotInitialized();
            if (!_propertyOffset.TryGetValue(propertyName, out var offset))
            {
                if (_target != null && targetMethod != null)
                {
                    targetMethod.Invoke(_target, new object[] { value });
                    return;
                }
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }

            object currentValue;
            if (_target != null)
                currentValue = _propertyCache[offset].GetValue(_target);
            else
                currentValue = Volatile.Read(ref _values[offset]);
            if (_reactivePropertyCache[offset].EqualsFunc(currentValue, value))
            {
                return;
            }

            Dependency dependency = _dependencies[offset];

            if (dependency != null)
                PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

            if (_target != null)
                _propertyCache[offset].SetValue(_target, value);
            else
                Interlocked.Exchange(ref _values[offset], value);

            if (dependency != null)
            {
                dependency.Trigger();
                if (PropertyChanged != null)
                {
                    TaskManager.QueueNotification(this, propertyName, (args) =>
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (!targetMethod.IsSpecialName)
            {
                if (targetMethod.DeclaringType == typeof(IReactive) && targetMethod.Name == nameof(IReactive.TrackDeep))
                {
                    TrackDeep();
                    return null;
                }
                ThrowIfNotInitialized();
                if (_target != null)
                {
                    return targetMethod.Invoke(_target, args);
                }
                throw new NotImplementedException($"Method '{targetMethod.Name}' is not implemented in ReactiveProxy.");
            }

            switch (targetMethod.Name)
            {
                case var name when name.StartsWith("get_"):
                    var propertyName = name.Substring(4);
                    return GetPropertyValue(propertyName);

                case var name when name.StartsWith("set_"):
                    propertyName = name.Substring(4);
                    SetPropertyValue(propertyName, args[0]);
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
