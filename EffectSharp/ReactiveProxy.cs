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
        private static readonly ReactiveProperty[] _reactivePropertyCache;

        static ReactiveProxy()
        {
            var type = typeof(T);
            _propertyCache = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            _propertyOffset = new Dictionary<string, int>(_propertyCache.Length);
            _reactivePropertyCache = new ReactiveProperty[_propertyCache.Length];
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                var prop = _propertyCache[i];
                _propertyOffset[prop.Name] = i;
                var attr = prop.GetCustomAttribute<ReactiveProperty>();
                if (attr == null)
                {
                    attr = new ReactiveProperty();
                }
                if (attr.Default == null && prop.PropertyType.IsValueType)
                {
                    attr.Default = Activator.CreateInstance(prop.PropertyType);
                }
                _reactivePropertyCache[i] = attr;
            }
        }

        private readonly (object Value, Dependency Dependency)[] _values;

        private static readonly AsyncLocal<bool> _isConstructing = new AsyncLocal<bool>();

        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public ReactiveProxy()
        {
            if (_isConstructing.Value)
                throw new InvalidOperationException($"Recursive construction of ReactiveProxy<{typeof(T).Name}> detected. Check the deep properties to avoid infinite recursion.");

            _isConstructing.Value = true;

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
                _isConstructing.Value = false;
            }
        }

        public void TrackDeep()
        {
            for (int i = 0; i < _values.Length; i++)
            {
                _values[i].Dependency?.Track();
                if (Volatile.Read(ref _values[i].Value) is IReactive reactiveValue)
                {
                    reactiveValue.TrackDeep();
                }
            }
        }

        public object GetPropertyValue(string propertyName)
        {
            if (_propertyOffset.TryGetValue(propertyName, out var offset))
            {
                _values[offset].Dependency?.Track();
                return Volatile.Read(ref _values[offset].Value);
            }
            throw new ArgumentException($"Property '{propertyName}' not found.");
        }

        public void SetPropertyValue(string propertyName, object value)
        {
            if (!_propertyOffset.TryGetValue(propertyName, out var offset))
                throw new ArgumentException($"Property '{propertyName}' not found.");

            if (_values[offset].Dependency == null)
            {
                Interlocked.Exchange(ref _values[offset].Value, value);
                return;
            }

            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));

            Interlocked.Exchange(ref _values[offset].Value, value);
            _values[offset].Dependency.Trigger();

            TaskManager.EnqueueNotification(this, propertyName, (args) =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            });
            return;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
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
