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
        private static PropertyInfo[] _propertyCache;
        private static ReactiveProperty[] _reactivePropertyCache;

        static ReactiveProxy()
        {
            var type = typeof(T);
            _propertyCache = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            _reactivePropertyCache = new ReactiveProperty[_propertyCache.Length];
            for (int i = 0; i < _propertyCache.Length; i++)
            {
                var prop = _propertyCache[i];
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

        private ConcurrentDictionary<string, (object Value, Dependency Dependency)> _propertyData =
            new ConcurrentDictionary<string, (object, Dependency)>();

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
                    _propertyData[prop.Name] = (initialValue, dep);
                }
            }
            finally
            {
                _isConstructing.Value = false;
            }
        }

        public void TrackDeep()
        {
            foreach (var data in _propertyData.Values)
            {
                data.Dependency?.Track();
                if (data.Value is IReactive reactive)
                {
                    reactive.TrackDeep();
                }
            }
        }

        public object GetPropertyValue(string propertyName)
        {
            if (_propertyData.TryGetValue(propertyName, out var data))
            {
                data.Dependency?.Track();
                return data.Value;
            }
            throw new ArgumentException($"Property '{propertyName}' not found.");
        }

        public void SetPropertyValue(string propertyName, object value)
        {
            if (!_propertyData.TryGetValue(propertyName, out var data))
                throw new ArgumentException($"Property '{propertyName}' not found.");

            if (data.Dependency == null)
            {
                data.Value = value;
                _propertyData[propertyName] = data;
                return;
            }

            PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
            data.Value = value;
            _propertyData[propertyName] = data;
            data.Dependency.Trigger();
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
