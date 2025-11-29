using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Provides methods to create and manage reactive objects, collections, and effects.
    /// </summary>
    public static class Reactive
    {
        private static readonly ProxyGenerator _proxyGenerator = new ProxyGenerator();

        public static T Create<T>(T target) where T : class
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target is IReactive) return target;
            if (!ReactiveInterceptor.CanProxyType(target.GetType()))
                throw new InvalidOperationException($"Cannot create reactive proxy for type '{target.GetType().FullName}'. It must be a non-sealed class.");

            return (T)CreateInternal(target);
        }

        public static T TryCreate<T>(T target)
        {
            if (target == null || target is IReactive || !ReactiveInterceptor.CanProxyType(target.GetType()))
                return target;
            return (T)CreateInternal(target);
        }

        private static object CreateInternal(object target)
        {
            Type[] types = { typeof(INotifyPropertyChanging), typeof(INotifyPropertyChanged), typeof(IReactive) };
            var interceptor = new ReactiveInterceptor();
            var proxy = _proxyGenerator.CreateClassProxyWithTarget(
                target.GetType(),
                types,
                target,
                interceptor);
            return proxy;
        }

        public static T CreateDeep<T>(T target) where T : class
        {
            var root = Create(target);
            if (root is IReactive reactive)
            {
                reactive.SetDeep();
            }
            return root;
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

        public static Ref<T> Ref<T>(T initialValue, bool deep = false)
        {
            if (initialValue == null)
                throw new ArgumentNullException(nameof(initialValue));

            var refObj = new Ref<T>(initialValue);
            if (deep)
            {
                refObj.SetDeep();
            }
            return refObj;
        }

        public static Effect Effect(Action action, Action<Effect> scheduler = null, bool lazy = false)
        {
            return new Effect(action, scheduler, lazy);
        }

        public static Computed<T> Computed<T>(Func<T> getter, Action setter = null)
        {
            return new Computed<T>(getter, setter);
        }

        public static Effect Watch<T>(Func<T> getter, Action<T, T> callback, WatchOptions<T> options = null)
        {
            if (options == null) options = WatchOptions<T>.Default;

            T oldValue = default;
            bool firstRun = true;

            return Effect(() =>
            {
                T newValue = getter();
                if (firstRun)
                {
                    oldValue = newValue;
                }

                if (options.Deep)
                {
                    if (newValue is IReactive reactive)
                    {
                        reactive.TrackDeep();
                    }
                    else if (newValue is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            if (item is IReactive itemReactive)
                            {
                                itemReactive.TrackDeep();
                            }
                        }
                    }
                }

                else if (options.EqualityComparer.Equals(oldValue, newValue) && !(firstRun && options.Immediate))
                {
                    firstRun = false;
                    return;
                }

                if (firstRun)
                {
                    firstRun = false;
                    if (!options.Immediate) 
                        return;
                }

                EffectSharp.Effect.Untracked(() =>
                {
                    callback(newValue, oldValue);
                    oldValue = newValue;
                });
            });
        }

        public static Effect Watch<T>(IRef<T> source, Action<T, T> callback, WatchOptions<T> options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Watch(() => source.Value, callback, options);
        }

        /// <summary>
        /// Binds the contents of an observable collection to a reactive list reference,
        /// synchronizing changes using a key selector to identify items.
        /// </summary>
        /// <typeparam name="T">The type of elements contained in the source list and observable collection.</typeparam>
        /// <typeparam name="TList">The type of the source list, which must implement <see cref="IList{T}"/>.</typeparam>
        /// <typeparam name="TKey">The type of the key used to identify elements for diffing.</typeparam>
        /// <param name="source">
        /// A reactive reference to the source list whose changes will be observed and reflected in the observable collection.
        /// Cannot be null.
        /// </param>
        /// <param name="observableCollection">
        /// The <see cref="ObservableCollection{T}"/> to be synchronized with the source list. Cannot be null.
        /// </param>
        /// <param name="keySelector">
        /// A function that extracts the unique key from each element, used to determine identity during diffing. Cannot be null.
        /// </param>
        /// <param name="equalityComparer">
        /// An optional equality comparer for keys. If null, the default equality comparer for <typeparamref name="TKey"/> is used.
        /// </param>
        /// <returns>
        /// An <see cref="IDisposable"/> that, when disposed, stops synchronizing changes from the source list to the observable collection.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="source"/>, <paramref name="observableCollection"/>, or <paramref name="keySelector"/> is null.
        /// </exception>
        public static IDisposable DiffAndBindTo<T, TList, TKey>(
            this IRef<TList> source,
            ObservableCollection<T> observableCollection,
            Func<T, TKey> keySelector,
            IEqualityComparer<TKey> equalityComparer = null)
            where TList : IList<T>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableCollection == null) throw new ArgumentNullException(nameof(observableCollection));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (equalityComparer == null) equalityComparer = EqualityComparer<TKey>.Default;

            return Watch(source, (newList, _) =>
            {
                ListSynchronizer.SyncKeyed(observableCollection, newList, keySelector, equalityComparer);
            }, new WatchOptions<TList> { Immediate = true });
        }

        /// <summary>
        /// Binds the contents of an observable collection to a reactive list reference,
        /// synchronizing changes without using keys.
        /// </summary>
        /// <typeparam name="T">The type of elements contained in the list and observable collection.</typeparam>
        /// <typeparam name="TList">The type of the referenced list, which must implement <see cref="IList{T}"/>.</typeparam>
        /// <param name="source">
        /// A reactive reference to the source list whose changes will be observed and reflected in the observable collection.
        /// Cannot be null.
        /// </param>
        /// <param name="observableCollection">
        /// The observable collection to be synchronized with the source list. Cannot be null.
        /// </param>
        /// <param name="equalityComparer">
        /// An optional equality comparer used to determine whether items are equal. If null, the default equality
        /// comparer for type <typeparamref name="T"/> is used.
        /// </param>
        /// <returns>
        /// An <see cref="IDisposable"/> that, when disposed, stops synchronizing changes from the source list to the observable collection.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="observableCollection"/> is null.</exception>
        public static IDisposable DiffAndBindTo<T, TList>(
            this IRef<TList> source,
            ObservableCollection<T> observableCollection,
            IEqualityComparer<T> equalityComparer = null)
            where TList : IList<T>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableCollection == null) throw new ArgumentNullException(nameof(observableCollection));
            if (equalityComparer == null) equalityComparer = EqualityComparer<T>.Default;

            return Watch(source, (newList, _) =>
            {
                ListSynchronizer.SyncUnkeyed(observableCollection, newList, equalityComparer);
            }, new WatchOptions<TList> { Immediate = true });
        }

        /// <summary>
        /// Asynchronously waits until the next effect and notification ticks have been processed.
        /// </summary>
        public static async Task NextTick()
        {
            await Task.WhenAll(TaskManager.NextEffectTick(), TaskManager.NextNotifyTick());
        }
    }

    public delegate void WatchCallback<T>(T newValue, T oldValue);

    public class WatchOptions<T>
    {
        public bool Immediate { get; set; } = false;
        public bool Deep { get; set; } = false;

        public IEqualityComparer<T> EqualityComparer { get; set; } = EqualityComparer<T>.Default;

        public static readonly WatchOptions<T> Default = new WatchOptions<T>();
    }
}
