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

        public static Ref<T> Ref<T>(T initialValue, bool deep = false)
        {
            if (initialValue == null)
                throw new ArgumentNullException(nameof(initialValue));

            return new Ref<T>(deep ? (T)CreateDeep((object)initialValue) : initialValue);
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

        /// <summary>
        /// Synchronizes an observable collection with the contents of a source collection, applying minimal changes to
        /// reflect updates, insertions, and deletions based on item identity and content.
        /// </summary>
        /// <remarks>This method efficiently updates the target observable collection by computing the
        /// minimal set of changes required to match the source collection, including insertions, deletions, and updates
        /// to item content. Item identity is determined by the idSelector function, and content changes are detected
        /// using the specified contentComparer. The synchronization continues until the returned IDisposable is
        /// disposed. Thread safety is not guaranteed; callers should ensure appropriate synchronization if collections
        /// are accessed from multiple threads.</remarks>
        /// <typeparam name="T">The type of elements contained in the collections.</typeparam>
        /// <typeparam name="TCollection">The type of the source collection, which must implement ICollection&lt;T&gt;.</typeparam>
        /// <typeparam name="TId">The type used to uniquely identify each element in the collections.</typeparam>
        /// <param name="source">A reference to the source collection whose changes will be observed and reflected in the target observable
        /// collection. Cannot be null.</param>
        /// <param name="observableCollection">The target observable collection to be updated in response to changes in the source collection. Cannot be
        /// null.</param>
        /// <param name="idSelector">A function that returns the unique identifier for a given element. Used to match items between the source
        /// and target collections. Cannot be null.</param>
        /// <param name="contentComparer">An equality comparer used to determine whether the content of two items with the same identifier is
        /// equivalent. If null, the default equality comparer for type T is used.</param>
        /// <returns>An IDisposable that, when disposed, stops synchronizing changes from the source collection to the observable
        /// collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown if source, observableCollection, or idSelector is null.</exception>
        public static IDisposable DiffAndBindToCollection<T, TCollection, TId>(
            this IRef<TCollection> source,
            ObservableCollection<T> observableCollection,
            Func<T, TId> idSelector,
            IEqualityComparer<T> contentComparer = null)
            where TCollection : IEnumerable<T>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableCollection == null) throw new ArgumentNullException(nameof(observableCollection));
            if (idSelector == null) throw new ArgumentNullException(nameof(idSelector));
            if (contentComparer == null)
            {
                contentComparer = EqualityComparer<T>.Default;
            }

            return Watch(source, (newCollection, _) =>
            {
                if (newCollection == null)
                {
                    observableCollection.Clear();
                    return;
                }

                // --- Conpute the diff ---
                // Define how to determine if two elements "match" (i.e., have the same ID)
                bool AreItemsMatching(T oldItem, T newItem)
                {
                    return EqualityComparer<TId>.Default.Equals(idSelector(oldItem), idSelector(newItem));
                }

                // Compute the minimal set of edits required to transform the current observable collection into the new collection
                // Reverse to avoid index shifting issues during edit application
                var edits = SequenceDiffer.ComputeDiff(observableCollection, newCollection, AreItemsMatching, true);

                // --- Apply deletions ---
                foreach (var edit in edits)
                {
                    switch (edit.Type)
                    {
                        case EditType.Delete:
                            observableCollection.RemoveAt(edit.Index);
                            break;
                        case EditType.Insert:
                            observableCollection.Insert(edit.Index, edit.Item);
                            break;
                    }
                }

                // --- Update existing items if their content has changed ---
                var i = 0;
                foreach (var newItem in newCollection)
                {
                    var existingItem = observableCollection[i];
                    if (!contentComparer.Equals(existingItem, newItem))
                    {
                        observableCollection[i] = newItem;
                    }
                    i++;
                }

            }, new WatchOptions { Immediate = true });
        }

        /// <summary>
        /// Synchronizes the contents of an observable collection with the items in a source collection reference,
        /// updating the observable collection to reflect changes in the source.
        /// </summary>
        /// <remarks>This method automatically updates the observable collection to reflect additions,
        /// removals, or replacements in the source collection reference. Thread safety is not guaranteed; callers
        /// should ensure appropriate synchronization if accessing collections from multiple threads.</remarks>
        /// <typeparam name="T">The type of elements contained in the collections.</typeparam>
        /// <typeparam name="TCollection">The type of the source collection, which must implement ICollection&lt;T&gt;.</typeparam>
        /// <param name="source">A reference to the source collection whose items will be mirrored in the observable collection. Cannot be
        /// null.</param>
        /// <param name="observableCollection">The observable collection to update so that its contents match those of the source collection. Cannot be
        /// null.</param>
        /// <param name="itemComparer">An optional equality comparer used to determine whether items in the collections are considered equal. If
        /// null, the default equality comparer for type T is used.</param>
        /// <returns>An IDisposable that, when disposed, stops synchronizing changes from the source collection to the observable
        /// collection.</returns>
        public static IDisposable DiffAndBindToCollection<T, TCollection>(
            this IRef<TCollection> source,
            ObservableCollection<T> observableCollection,
            IEqualityComparer<T> itemComparer = null)
            where TCollection : IEnumerable<T>
        {
            return DiffAndBindToCollection(
                source,
                observableCollection,
                item => item,
                itemComparer);
        }

        public static async Task NextTick()
        {
            await DependencyTracker.NextTick();
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
