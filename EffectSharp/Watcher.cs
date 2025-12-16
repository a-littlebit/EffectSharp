using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp
{
    public static class Watcher
    {
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

            return new Effect(() =>
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

                Effect.Untracked(() =>
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
    }

    /// <summary>
    /// Options that control the behavior of <see cref="Watcher.Watch{T}(Func{T}, Action{T, T}, WatchOptions{T})"/>.
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
