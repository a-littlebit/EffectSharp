using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp
{
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// A thread-safe set implementation using ConcurrentDictionary.
    /// </summary>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
    public class ConcurrentSet<T> : IReadOnlyCollection<T>
    {
        private readonly ConcurrentDictionary<T, object> _dictionary = new ConcurrentDictionary<T, object>();
        private static readonly object _placeholder = new object();

        /// <summary>
        /// Adds an element to the set (if not already present, returns whether added)
        /// </summary>
        public bool Add(T item)
        {
            return _dictionary.TryAdd(item, _placeholder);
        }

        /// <summary>
        /// Removes an element from the set. Returns whether the element was present and removed.
        /// </summary>
        public bool Remove(T item)
        {
            return _dictionary.TryRemove(item, out _);
        }

        /// <summary>
        /// Determines whether the set contains a specific element.
        /// </summary>
        public bool Contains(T item)
        {
            return _dictionary.ContainsKey(item);
        }

        /// <summary>
        /// Clears all elements from the set.
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
        }

        /// <summary>
        /// Gets the number of elements in the set.
        /// </summary>
        public int Count => _dictionary.Count;

        /// <summary>
        /// Enumerates the elements in the set.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return _dictionary.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
