using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace EffectSharp
{
    public class ReactiveProxy<T> : DispatchProxy, IReactive, INotifyPropertyChanging, INotifyPropertyChanged
        where T : class
    {
        private static readonly PropertyInfo[] _propertyCache;
        private static readonly Dictionary<string, int> _propertyOffset;
        private static readonly ReactivePropertyAttribute[] _reactivePropertyCache;
        private static readonly ConditionalWeakTable<T, Dependency[]> _attachedDependencies
            = new ConditionalWeakTable<T, Dependency[]>();

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

        private static Dependency[] InitializeAttachedDependencies(T target)
        {
            var dependencies = new Dependency[_propertyCache.Length];
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                var reactiveAttr = _reactivePropertyCache[i];
                if (reactiveAttr.Reactive)
                {
                    dependencies[i] = new Dependency();
                }
            }
            return dependencies;
        }

        private static Dependency[] GetOrCreateAttachedDependencies(T target)
        {
            return _attachedDependencies.GetValue(target, InitializeAttachedDependencies);
        }

        private static Dependency GetOrCreateAttachedDependency(T target, string propertyName)
        {
            if (_propertyOffset.TryGetValue(propertyName, out var offset))
            {
                return GetOrCreateAttachedDependencies(target)[offset];
            }
            throw new ArgumentException($"Property '{propertyName}' not found.");
        }

        private (object Value, Dependency Dependency)[] _values;
        private T _target;

        private static readonly AsyncLocal<bool> _isInitializing = new AsyncLocal<bool>();

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public ReactiveProxy()
        {
        }

        public void InitializeForTarget(T target)
        {
            _target = target;
        }

        public void InitializeForValues()
        {
            if (_isInitializing.Value)
                throw new InvalidOperationException($"Recursive construction of ReactiveProxy<{typeof(T).Name}> detected. Check the deep properties to avoid infinite recursion.");

            _isInitializing.Value = true;

            try
            {
                _values = new (object Value, Dependency Dependency)[_propertyCache.Length];
                for (int i = 0; i < _propertyCache.Length; i++)
                {
                    var prop = _propertyCache[i];
                    var reactiveAttr = _reactivePropertyCache[i];

                    object initialValue;
                    if (reactiveAttr.Deep && prop.PropertyType.IsInterface)
                        initialValue = Reactive.Create(prop.PropertyType);
                    else
                        initialValue = reactiveAttr.Default;

                    var dep = reactiveAttr.Reactive ? new Dependency() : null;
                    _values[i] = (initialValue, dep);
                }
            }
            finally
            {
                _isInitializing.Value = false;
            }
        }

        private void ThrowIfNotInitialized()
        {
            if (_target == null && _values == null)
            {
                throw new InvalidOperationException($"ReactiveProxy<{typeof(T).Name}> is not initialized. Call InitializeForTarget or InitializeForValues before using it.");
            }
        }

        public void TrackDeep()
        {
            ThrowIfNotInitialized();
            if (_target != null)
            {
                var attachedDeps = GetOrCreateAttachedDependencies(_target);
                for (int i = 0; i < attachedDeps.Length; i++)
                {
                    attachedDeps[i]?.Track();
                    var value = _propertyCache[i].GetValue(_target);
                    if (value != null && value is IReactive reactiveValue)
                    {
                        reactiveValue.TrackDeep();
                    }
                }
            }
            else
            {
                for (int i = 0; i < _values.Length; i++)
                {
                    _values[i].Dependency?.Track();
                    var value = Volatile.Read(ref _values[i].Value);
                    if (value != null && value is IReactive reactiveValue)
                    {
                        reactiveValue.TrackDeep();
                    }
                }
            }
        }

        public object GetPropertyValue(string propertyName)
        {
            ThrowIfNotInitialized();
            if (!_propertyOffset.TryGetValue(propertyName, out var offset))
            {
                throw new ArgumentException($"Property '{propertyName}' not found.");
            }
            if (_target != null)
            {
                GetOrCreateAttachedDependencies(_target)[offset]?.Track();
                return _propertyCache[offset].GetValue(_target);
            }
            else
            {
                _values[offset].Dependency?.Track();
                return Volatile.Read(ref _values[offset].Value);
            }
        }

        public void SetPropertyValue(string propertyName, object value)
        {
            ThrowIfNotInitialized();
            if (!_propertyOffset.TryGetValue(propertyName, out var offset))
                throw new ArgumentException($"Property '{propertyName}' not found.");

            Dependency dependency;
            if (_target != null)
                dependency = GetOrCreateAttachedDependencies(_target)[offset];
            else
                dependency = _values[offset].Dependency;
            if (dependency == null)
            {
                Interlocked.Exchange(ref _values[offset].Value, value);
                return;
            }

            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

            if (_target != null)
                _propertyCache[offset].SetValue(_target, value);
            else
                Interlocked.Exchange(ref _values[offset].Value, value);
            dependency.Trigger();

            TaskManager.EnqueueNotification(this, propertyName, (args) =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
            return;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ThrowIfNotInitialized();
            switch (targetMethod.Name)
            {
                case nameof(IReactive.TrackDeep):
                    TrackDeep();
                    return null;

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
