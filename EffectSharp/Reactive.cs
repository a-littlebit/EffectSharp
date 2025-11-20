using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace EffectSharp
{
    /// <summary>
    /// Provides methods to create and manage reactive objects, collections, and effects.
    /// </summary>
    public static class Reactive
    {
        private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        public static bool CanCreate(Type type)
        {
            return type.IsClass && !type.IsSealed;
        }

        public static T Create<T>(T target) where T : class
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target is IReactive) return target;
            if (!CanCreate(target.GetType()))
                throw new InvalidOperationException($"Cannot create reactive proxy for type '{target.GetType().FullName}'. It must be a non-sealed class.");

            Type[] types = { typeof(INotifyPropertyChanging), typeof(INotifyPropertyChanged), typeof(IReactive) };
            var interceptor = new ReactiveInterceptor();
            var proxy = _proxyGenerator.CreateClassProxyWithTarget(
                target.GetType(),
                types,
                target,
                interceptor);
            return (T)proxy;
        }

        public static T CreateDeep<T>(T target) where T : class
        {
            var root = Create(target);
            WrapNestedProperties(target);
            return root;

            void WrapNestedProperties(object obj)
            {
                var objType = obj.GetType();
                foreach (var propertyInfo in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!propertyInfo.CanRead || !propertyInfo.CanWrite) continue;

                    var value = propertyInfo.GetValue(obj);
                    if (value == null || value is IReactive || !CanCreate(value.GetType())) continue;

                    var reactiveValue = Create(value);
                    propertyInfo.SetValue(obj, reactiveValue);
                }
            }
        }

        public static ReactiveCollection<T> Collection<T>()
        {
            return new ReactiveCollection<T>();
        }

        public static ReactiveCollection<T> Collection<T>(IEnumerable<T> collection)
        {
            return new ReactiveCollection<T>(collection);
        }

        public static ReactiveCollection<T> Collection<T>(List<T> list)
        {
            return new ReactiveCollection<T>(list);
        }

        public static ReactiveDictionary<TKey, TValue> Dictionary<TKey, TValue>()
        {
            return new ReactiveDictionary<TKey, TValue>();
        }

        public static Ref<T> Ref<T>(T initialValue)
        {
            if (initialValue == null)
                throw new ArgumentNullException(nameof(initialValue));

            return new Ref<T>(!(initialValue is IReactive) && CanCreate(typeof(T))
                ? (T)CreateDeep((object)initialValue)
                : initialValue);
        }

        public static Effect Effect(Action action, Action<Effect> scheduler, bool lazy = false)
        {
            return new Effect(action, scheduler, lazy);
        }

        public static Computed<T> Computed<T>(Func<T> getter, Action setter = null)
        {
            return new Computed<T>(getter, setter);
        }

        public static IDisposable Watch(IReactive reactive, string propertyName, Action callback, WatchOptions options = null)
        {
            if (options == null) options = WatchOptions.Default;

            var effect = new Effect(() => { }, (_) => callback(), true);

            var allDeps = new List<Dependency>();
            if (options.Deep)
            {
                GetNestedDependencies(reactive, propertyName, allDeps);
                if (allDeps.Count == 0)
                    throw new ArgumentException($"Property '{propertyName}' is not reactive.", nameof(propertyName));
                foreach (var nestedDep in allDeps)
                {
                    nestedDep.AddSubscriber(effect);
                }
            }
            else
            {
                var dep = reactive.GetDependency(propertyName);
                if (dep == null)
                    throw new ArgumentException($"Property '{propertyName}' is not reactive.", nameof(propertyName));
                allDeps.Add(dep);
                dep.AddSubscriber(effect);
            }

            if (options.Immediate)
            {
                callback();
            }
            return new Unsubcriber(() =>
            {
                foreach (var dep in allDeps)
                {
                    dep.RemoveSubscriber(effect);
                }
                effect.Dispose();
            });

            void GetNestedDependencies(IReactive root, string rootProp, List<Dependency> deps)
            {
                var dep = root.GetDependency(rootProp);
                if (dep == null) return;
                deps.Add(dep);
                var prop = root.GetType().GetProperty(rootProp);
                if (prop == null || !prop.CanRead) return;

                var value = prop.GetValue(root);
                if (value == null || !(value is IReactive)) return;

                var valueProps = value?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (valueProps == null) return;
                foreach (var valueProp in valueProps)
                {
                    GetNestedDependencies((IReactive)value, valueProp.Name, deps);
                }
            }
        }

        public static IDisposable Watch<T>(IReactive reactive, string propertyName, WatchCallback<T> callback, WatchOptions options = null)
        {
            var prop = reactive.GetType().GetProperty(propertyName);
            if (prop == null || !prop.CanRead)
                throw new ArgumentException($"Property '{propertyName}' is not readable.", nameof(propertyName));


            bool immediate = options?.Immediate ?? false;
            var oldValue = prop.GetValue(reactive);
            if (oldValue != null && !(oldValue is T))
                throw new ArgumentException($"Property '{propertyName}' is not of type {typeof(T).Name}.", nameof(propertyName));

            return Watch(reactive, propertyName, () =>
            {
                var newValue = immediate ? oldValue : prop.GetValue(reactive);
                if (immediate || !Equals(oldValue, newValue))
                {
                    callback((T)newValue, (T)oldValue);
                    oldValue = newValue;
                    immediate = false;
                }
            }, options);
        }

        public static IDisposable Watch<T>(IRef<T> reactiveRef, Action callback, WatchOptions options = null)
        {
            return Watch(reactiveRef, nameof(reactiveRef.Value), callback, options);
        }

        public static IDisposable Watch<T>(IRef<T> reactiveRef, WatchCallback<T> callback, WatchOptions options = null)
        {
            return Watch(reactiveRef, nameof(reactiveRef.Value), callback, options);
        }

        public static IDisposable Watch<T>(Func<T> getter, Action callback, WatchOptions options = null)
        {
            var computed = Computed(getter);
            var sub = Watch(computed, nameof(computed.Value), callback, options);
            return new Unsubcriber(() =>
            {
                computed.Dispose();
                sub.Dispose();
            });
        }

        public static IDisposable Watch<T>(Func<T> getter, WatchCallback<T> callback, WatchOptions options = null)
        {
            var computed = Computed(getter);
            var sub = Watch(computed, nameof(computed.Value), callback, options);
            return new Unsubcriber(() =>
            {
                computed.Dispose();
                sub.Dispose();
            });
        }
    }

    public delegate void WatchCallback<T>(T newValue, T oldValue);

    public class WatchOptions
    {
        public bool Immediate { get; set; } = false;
        public bool Deep { get; set; } = false;

        public static readonly WatchOptions Default = new WatchOptions();
    }

    internal class Unsubcriber : IDisposable
    {
        private readonly Action _unsubscribeAction;
        private bool _isDisposed = false;
        public Unsubcriber(Action unsubscribeAction)
        {
            _unsubscribeAction = unsubscribeAction;
        }
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _unsubscribeAction();
                _isDisposed = true;
            }
        }
    }
}
