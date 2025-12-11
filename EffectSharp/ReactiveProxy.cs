using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading;

namespace EffectSharp
{
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
                var attr = prop.GetCustomAttribute<ReactivePropertyAttribute>();
                if (attr == null)
                {
                    attr = new ReactivePropertyAttribute();
                }
                if (attr.Default == null && prop.PropertyType.IsValueType)
                {
                    attr.Default = Activator.CreateInstance(prop.PropertyType);
                }
                _reactivePropertyCache[i] = attr;
            }
        }

        private Dependency[] _dependencies;
        private object[] _values;
        private T _target;

        private static readonly ThreadLocal<bool> _isInitializing = new ThreadLocal<bool>();

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public T Target => _target;

        public ReactiveProxy()
        {
        }

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

                TaskManager.EnqueueNotification(this, propertyName, (args) =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
                });
            }
        }

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
