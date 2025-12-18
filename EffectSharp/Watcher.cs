using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp
{
    public static class Watcher
    {
        /// <summary>
        /// Watches a value produced by <paramref name="getter"/> and invokes <paramref name="callback"/>
        /// </summary>
        /// <typeparam name="T">The watched value type.</typeparam>
        /// <param name="getter">Function that returns the value to watch.</param>
        /// <param name="callback">Callback receiving the new and previous values.</param>
        /// <param name="immediate">When true, the callback is invoked during the first evaluation. Default is <c>false</c>.</param>
        /// <param name="deep">
        /// When true, performs deep tracking: if the getter returns an <see cref="IReactive"/> value (or an enumerable of reactive items),
        /// their nested dependencies are tracked, and the equality short-circuit is disabled. Default is <c>false</c>.
        /// </param>
        /// <param name="once">When true, the watcher is automatically disposed after the first callback invocation. Default is <c>false</c>.</param>
        /// <param name="scheduler">Optional scheduler for the underlying effect; if provided, it receives the created <see cref="Effect"/> instance.</param>
        /// <param name="suppressEquality">
        /// When true, uses <paramref name="equalityComparer"/> to suppress callbacks when the produced value has not changed.
        /// Default is <c>true</c>. Ignored when <paramref name="deep"/> is true.
        /// </param>
        /// <param name="equalityComparer">
        /// Equality comparer used to suppress callbacks when the produced value has not changed.
        /// Default is <see cref="EqualityComparer{T}.Default"/>.
        /// </param>
        /// <returns>An <see cref="Effect"/> representing the watch; dispose to stop watching.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="getter"/> or <paramref name="callback"/> is null.</exception>
        public static Effect Watch<T>(
            Func<T> getter,
            Action<T, T> callback,
            bool immediate = false,
            bool deep = false,
            bool once = false,
            Action<Effect> scheduler = null,
            bool suppressEquality = true,
            IEqualityComparer<T> equalityComparer = null)
        {
            if (getter == null) throw new ArgumentNullException(nameof(getter));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (suppressEquality && equalityComparer == null)
                equalityComparer = EqualityComparer<T>.Default;

            T oldValue = default;
            bool firstRun = true;

            return new Effect(() =>
            {
                T newValue = getter();

                if (deep)
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
                else if (suppressEquality && equalityComparer.Equals(oldValue, newValue) && !firstRun)
                {
                    return;
                }

                if (firstRun)
                {
                    firstRun = false;
                    if (!immediate)
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

                if (once)
                {
                    Effect.Current.Dispose();
                }
            }, scheduler);
        }

        /// <summary>
        /// Watches the value of a reactive reference and invokes <paramref name="callback"/>
        /// </summary>
        /// <typeparam name="T">The watched value type.</typeparam>
        /// <param name="source">The reactive reference to watch.</param>
        /// <param name="callback">Callback receiving the new and previous values.</param>
        /// <param name="immediate">When true, the callback is invoked during the first evaluation. Default is <c>false</c>.</param>
        /// <param name="deep">
        /// When true, performs deep tracking: if the getter returns an <see cref="IReactive"/> value (or an enumerable of reactive items),
        /// their nested dependencies are tracked, and the equality short-circuit is disabled. Default is <c>false</c>.
        /// </param>
        /// <param name="once">When true, the watcher is automatically disposed after the first callback invocation. Default is <c>false</c>.</param>
        /// <param name="scheduler">Optional scheduler for the underlying effect; if provided, it receives the created <see cref="Effect"/> instance.</param>
        /// <param name="suppressEquality">
        /// When true, uses <paramref name="equalityComparer"/> to suppress callbacks when the produced value has not changed.
        /// Default is <c>true</c>. Ignored when <paramref name="deep"/> is true.
        /// </param>
        /// <param name="equalityComparer">
        /// Equality comparer used to suppress callbacks when the produced value has not changed.
        /// Default is <see cref="EqualityComparer{T}.Default"/>.
        /// </param>
        /// <returns>An <see cref="Effect"/> representing the watch; dispose to stop watching.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="callback"/> is null.</exception>
        public static Effect Watch<T>(
            IRef<T> source,
            Action<T, T> callback,
            bool immediate = false,
            bool deep = false,
            bool once = false,
            Action<Effect> scheduler = null,
            bool suppressEquality = true,
            IEqualityComparer<T> equalityComparer = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Watch(() => source.Value, callback, immediate, deep, once, scheduler, suppressEquality, equalityComparer);
        }
    }
}
