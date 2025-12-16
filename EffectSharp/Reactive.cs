using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Provides methods to create and manage reactive objects, collections, and effects.
    /// </summary>
    public static class Reactive
    {
        private static readonly MethodInfo _createMethod = typeof(Reactive).GetMethod(nameof(Create), Array.Empty<Type>());

        /// <summary>
        /// Creates a new reactive proxy instance of <typeparamref name="T"/> that stores values internally.
        /// Properties are reactive by default unless annotated with <see cref="ReactivePropertyAttribute"/> specifying otherwise.
        /// Interface-typed properties marked with <see cref="ReactivePropertyAttribute.Deep"/> are initialized as nested reactive proxies.
        /// </summary>
        /// <typeparam name="T">The interface or class type to proxy. Must be a reference type.</typeparam>
        /// <returns>A reactive proxy implementing <typeparamref name="T"/>.</returns>
        public static T Create<T>() where T : class
        {
            var proxy = DispatchProxy.Create<T, ReactiveProxy<T>>();
            (proxy as ReactiveProxy<T>).InitializeForValues();
            return proxy;
        }

        /// <summary>
        /// Creates a reactive proxy that delegates property access to the specified <paramref name="instance"/>.
        /// Properties participate in dependency tracking by default unless marked with <see cref="ReactivePropertyAttribute.Reactive"/> = <c>false</c> on the interface.
        /// </summary>
        /// <typeparam name="T">The interface or class type to proxy. Must be a reference type.</typeparam>
        /// <param name="instance">The target instance to delegate to.</param>
        /// <returns>A reactive proxy wrapping the provided instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
        public static T Create<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var proxy = DispatchProxy.Create<T, ReactiveProxy<T>>();
            (proxy as ReactiveProxy<T>).InitializeForTarget(instance);
            return proxy;
        }

        /// <summary>
        /// Creates a new reactive proxy for the specified reference <paramref name="type"/> using reflection.
        /// Equivalent to calling the generic <see cref="Create{T}()"/> with <paramref name="type"/> as &lt;T&gt;.
        /// </summary>
        /// <param name="type">The reference type to proxy.</param>
        /// <returns>A reactive proxy instance implementing the specified type.</returns>
        public static object Create(Type type)
        {
            var method = _createMethod.MakeGenericMethod(type);
            return method.Invoke(null, null);
        }

        /// <summary>
        /// Creates an empty <see cref="ReactiveCollection{T}"/>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <returns>A new reactive collection.</returns>
        public static ReactiveCollection<T> Collection<T>()
        {
            return new ReactiveCollection<T>();
        }

        /// <summary>
        /// Creates a <see cref="ReactiveCollection{T}"/> initialized with the items from <paramref name="collection"/>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="collection">Source items used to populate the collection.</param>
        /// <returns>A new reactive collection containing the provided items.</returns>
        public static ReactiveCollection<T> Collection<T>(IEnumerable<T> collection)
        {
            return new ReactiveCollection<T>(collection);
        }

        /// <summary>
        /// Creates a <see cref="ReactiveCollection{T}"/> initialized with the contents of <paramref name="list"/>.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="list">Source list used to populate the collection.</param>
        /// <returns>A new reactive collection containing the list's items.</returns>
        public static ReactiveCollection<T> Collection<T>(List<T> list)
        {
            return new ReactiveCollection<T>(list);
        }

        /// <summary>
        /// Creates an empty <see cref="ReactiveDictionary{TKey, TValue}"/>.
        /// </summary>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TValue">Value type.</typeparam>
        /// <returns>A new reactive dictionary.</returns>
        public static ReactiveDictionary<TKey, TValue> Dictionary<TKey, TValue>()
        {
            return new ReactiveDictionary<TKey, TValue>();
        }

        /// <summary>
        /// Creates an empty <see cref="ReactiveDictionary{TKey, TValue}"/> with a custom key comparer.
        /// </summary>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TValue">Value type.</typeparam>
        /// <param name="comparer">The equality comparer used to compare keys.</param>
        /// <returns>A new reactive dictionary using the specified comparer.</returns>
        public static ReactiveDictionary<TKey, TValue> Dictionary<TKey, TValue>(IEqualityComparer<TKey> comparer)
        {
            return new ReactiveDictionary<TKey, TValue>(comparer);
        }

        /// <summary>
        /// Creates an empty <see cref="ReactiveDictionary{TKey, TValue}"/> with an initial capacity.
        /// </summary>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TValue">Value type.</typeparam>
        /// <param name="capacity">Initial capacity of the dictionary's internal storage.</param>
        /// <returns>A new reactive dictionary with the given capacity.</returns>
        public static ReactiveDictionary<TKey, TValue> Dictionary<TKey, TValue>(int capacity)
        {
            return new ReactiveDictionary<TKey, TValue>(capacity);
        }

        /// <summary>
        /// Creates an empty <see cref="ReactiveDictionary{TKey, TValue}"/> with an initial capacity and custom comparer.
        /// </summary>
        /// <typeparam name="TKey">Key type.</typeparam>
        /// <typeparam name="TValue">Value type.</typeparam>
        /// <param name="capacity">Initial capacity of the dictionary's internal storage.</param>
        /// <param name="comparer">The equality comparer used to compare keys.</param>
        /// <returns>A new reactive dictionary configured with capacity and comparer.</returns>
        public static ReactiveDictionary<TKey, TValue> Dictionary<TKey, TValue>(int capacity, IEqualityComparer<TKey> comparer)
        {
            return new ReactiveDictionary<TKey, TValue>(capacity, comparer);
        }

        /// <summary>
        /// Creates a reactive reference with the specified initial value and optional equality comparer.
        /// </summary>
        /// <typeparam name="T">The referenced value type.</typeparam>
        /// <param name="initialValue">Initial value stored in the reference.</param>
        /// <param name="equalityComparer">Optional equality comparer used to suppress no-op updates.</param>
        /// <returns>A new <see cref="Ref{T}"/>.</returns>
        public static Ref<T> Ref<T>(T initialValue = default, IEqualityComparer<T> equalityComparer = null)
        {
            return new Ref<T>(initialValue, equalityComparer);
        }

        /// <summary>
        /// Creates and returns a new <see cref="Effect"/> that executes the provided <paramref name="action"/>,
        /// tracking dependencies and re-running when they change. Optionally schedules execution via <paramref name="scheduler"/>.
        /// </summary>
        /// <param name="action">The effect body to execute.</param>
        /// <param name="scheduler">Optional scheduler invoked with the effect instance to control when it runs.</param>
        /// <param name="lazy">If true, defers the first execution until explicitly scheduled.</param>
        /// <returns>The created effect.</returns>
        public static Effect Effect(Action action, Action<Effect> scheduler = null, bool lazy = false)
        {
            return new Effect(action, scheduler, lazy);
        }

        /// <summary>
        /// Creates a <see cref="Computed{T}"/> value backed by the specified getter and optional setter.
        /// </summary>
        /// <typeparam name="T">Computed value type.</typeparam>
        /// <param name="getter">Function that produces the value and participates in dependency tracking.</param>
        /// <param name="setter">Optional setter invoked when the computed is assigned.</param>
        /// <returns>A new computed value.</returns>
        public static Computed<T> Computed<T>(Func<T> getter, Action<T> setter = null)
        {
            return new Computed<T>(getter, setter);
        }

        /// <summary>
        /// Watches a value produced by <paramref name="getter"/> and invokes <paramref name="callback"/>
        /// when it changes, with configurable behavior via <paramref name="options"/>.
        /// When <see cref="WatchOptions{T}.Immediate"/> is true, the callback is invoked on the first run.
        /// When <see cref="WatchOptions{T}.Deep"/> is true, any returned <see cref="IReactive"/> value (or reactive items in an enumerable)
        /// is tracked deeply and equality suppression is disabled.
        /// </summary>
        /// <typeparam name="T">The watched value type.</typeparam>
        /// <param name="getter">Function that returns the value to watch.</param>
        /// <param name="callback">Callback receiving the new and previous values.</param>
        /// <param name="options">Optional watch options controlling immediate, deep, equality, and scheduling behavior.</param>
        /// <returns>An <see cref="Effect"/> representing the watch; dispose to stop watching.</returns>
        public static Effect Watch<T>(Func<T> getter, Action<T, T> callback, WatchOptions<T> options = null)
        {
            if (options == null) options = WatchOptions<T>.Default;

            T oldValue = default;
            bool firstRun = true;

            return Effect(() =>
            {
                T newValue = getter();

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
                else if (options.EqualityComparer != null && options.EqualityComparer.Equals(oldValue, newValue) && !firstRun)
                {
                    return;
                }

                if (firstRun)
                {
                    firstRun = false;
                    if (!options.Immediate)
                    {
                        oldValue = newValue;
                        return;
                    }
                }

                EffectSharp.Effect.Untracked(() =>
                {
                    callback(newValue, oldValue);
                    oldValue = newValue;
                });
            }, options.Scheduler);
        }

        /// <summary>
        /// Watches the value of the reactive reference <paramref name="source"/> and invokes <paramref name="callback"/>
        /// when it changes. See <see cref="Watch{T}(Func{T}, Action{T, T}, WatchOptions{T})"/> for behavior details.
        /// </summary>
        /// <typeparam name="T">The referenced value type.</typeparam>
        /// <param name="source">The reactive reference to observe.</param>
        /// <param name="callback">Callback receiving the new and previous values.</param>
        /// <param name="options">Optional watch options.</param>
        /// <returns>An <see cref="Effect"/> representing the watch.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
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
        /// <param name="observableCollection">
        /// The <see cref="ObservableCollection{T}"/> to be synchronized with the source list. Cannot be null.
        /// </param>
        /// <param name="source">
        /// A function that returns the source list whose changes will be observed and reflected in the observable collection.
        /// </param>
        /// <param name="keySelector">
        /// A function that extracts the unique key from each element, used to determine identity during diffing. Cannot be null.
        /// </param>
        /// <param name="equalityComparer">
        /// An optional equality comparer for keys. If null, the default equality comparer for <typeparamref name="TKey"/> is used.
        /// </param>
        /// <param name="scheduler">
        /// An optional scheduler action to control when the synchronization effect is executed.
        /// </param>
        /// <returns>
        /// An <see cref="IDisposable"/> that, when disposed, stops synchronizing changes from the source list to the observable collection.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="source"/>, <paramref name="observableCollection"/>, or <paramref name="keySelector"/> is null.
        /// </exception>
        public static IDisposable BindTo<T, TList, TKey>(
            this ObservableCollection<T> observableCollection,
            Func<TList> source,
            Func<T, TKey> keySelector,
            IEqualityComparer<TKey> equalityComparer = null,
            Action<Effect> scheduler = null)
            where TList : IList<T>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableCollection == null) throw new ArgumentNullException(nameof(observableCollection));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (equalityComparer == null) equalityComparer = EqualityComparer<TKey>.Default;

            return Watch(source, (newList, _) =>
            {
                ListSynchronizer.SyncWithUnique(observableCollection, newList, keySelector, equalityComparer);
            }, new WatchOptions<TList> { Immediate = true, Scheduler = scheduler, EqualityComparer = null });
        }

        /// <summary>
        /// Binds the contents of an observable collection to a reactive list reference,
        /// synchronizing changes without using keys.
        /// </summary>
        /// <typeparam name="T">The type of elements contained in the list and observable collection.</typeparam>
        /// <typeparam name="TList">The type of the referenced list, which must implement <see cref="IList{T}"/>.</typeparam>
        /// <param name="observableCollection">
        /// The observable collection to be synchronized with the source list. Cannot be null.
        /// </param>
        /// <param name="source">
        /// A function that returns the source list whose changes will be observed and reflected in the observable collection.
        /// </param>
        /// <param name="equalityComparer">
        /// An optional equality comparer used to determine whether items are equal. If null, the default equality
        /// comparer for type <typeparamref name="T"/> is used.
        /// </param>
        /// <param name="scheduler">
        /// An optional scheduler action to control when the synchronization effect is executed.
        /// </param>
        /// <returns>
        /// An <see cref="IDisposable"/> that, when disposed, stops synchronizing changes from the source list to the observable collection.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> or <paramref name="observableCollection"/> is null.</exception>
        public static IDisposable BindTo<T, TList>(
            this ObservableCollection<T> observableCollection,
            Func<TList> source,
            IEqualityComparer<T> equalityComparer = null,
            Action<Effect> scheduler = null)
            where TList : IList<T>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableCollection == null) throw new ArgumentNullException(nameof(observableCollection));
            if (equalityComparer == null) equalityComparer = EqualityComparer<T>.Default;

            return Watch(source, (newList, _) =>
            {
                ListSynchronizer.SyncWith(observableCollection, newList, t => t, equalityComparer);
            }, new WatchOptions<TList> { Immediate = true, Scheduler = scheduler, EqualityComparer = null });
        }

        /// <summary>
        /// Creates a <see cref="ReactiveCollection{T}"/> that is computed from the specified getter function,
        /// synchronizing its contents based on the provided key selector.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <typeparam name="TList">The type of the source list, which must implement <see cref="IList{T}"/>.</typeparam>
        /// <typeparam name="TKey">The type of the key used to identify elements for diffing.</typeparam>
        /// <param name="getter">Function that returns the source list.</param>
        /// <param name="keySelector">Function that extracts the unique key from each element.</param>
        /// <param name="equalityComparer">Optional equality comparer for keys.</param>
        /// <returns>A new reactive collection bound to the computed list.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="getter"/> or <paramref name="keySelector"/> is null.</exception>
        public static ReactiveCollection<T> ComputedList<T, TList, TKey>(
            Func<TList> getter,
            Func<T, TKey> keySelector,
            IEqualityComparer<TKey> equalityComparer = null)
            where TList : IList<T>
        {
            var collection = new ReactiveCollection<T>();
            collection.BindTo(getter, keySelector, equalityComparer);
            return collection;
        }

        /// <summary>
        /// Creates a <see cref="ReactiveCollection{T}"/> that is computed from the specified getter function,
        /// synchronizing its contents without using keys.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <typeparam name="TList">The type of the source list, which must implement <see cref="IList{T}"/>.</typeparam>
        /// <param name="getter">Function that returns the source list.</param>
        /// <param name="equalityComparer">Optional equality comparer for items.</param>
        /// <returns>A new reactive collection bound to the computed list.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="getter"/> is null.</exception>
        public static ReactiveCollection<T> ComputedList<T, TList>(
            Func<TList> getter,
            IEqualityComparer<T> equalityComparer = null)
            where TList : IList<T>
        {
            var collection = new ReactiveCollection<T>();
            collection.BindTo(getter, equalityComparer);
            return collection;
        }

        /// <summary>
        /// Returns a task that completes on the next reactive tick, after all scheduled effects and notifications have been processed.
        /// </summary>
        public static Task NextTick(CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(TaskManager.NextEffectTick(cancellationToken), TaskManager.NextNotificationTick(cancellationToken));
        }
    }

    /// <summary>
    /// Options that control the behavior of <see cref="Reactive.Watch{T}(Func{T}, Action{T, T}, WatchOptions{T})"/>.
    /// </summary>
    /// <typeparam name="T">The watched value type.</typeparam>
    public class WatchOptions<T>
    {
        /// <summary>
        /// When true, the callback is invoked during the first evaluation. Default is <c>false</c>.
        /// </summary>
        public bool Immediate { get; set; } = false;
        /// <summary>
        /// When true, performs deep tracking: if the getter returns an <see cref="IReactive"/> value (or an enumerable of reactive items),
        /// their nested dependencies are tracked, and the equality short-circuit is disabled. Default is <c>false</c>.
        /// </summary>
        public bool Deep { get; set; } = false;
        /// <summary>
        /// Equality comparer used to suppress callbacks when the produced value has not changed. Default is <see cref="EqualityComparer{T}.Default"/>.
        /// Ignored when <see cref="Deep"/> is true.
        /// </summary>
        public IEqualityComparer<T> EqualityComparer { get; set; } = EqualityComparer<T>.Default;
        /// <summary>
        /// Optional scheduler for the underlying effect; if provided, it receives the created <see cref="Effect"/> instance.
        /// </summary>
        public Action<Effect> Scheduler { get; set; } = null;

        /// <summary>
        /// A reusable default options instance.
        /// </summary>
        public static readonly WatchOptions<T> Default = new WatchOptions<T>();
    }
}
