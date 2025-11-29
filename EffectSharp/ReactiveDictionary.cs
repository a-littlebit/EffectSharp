using EffectSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EffectSharp
{
    /// <summary>
    /// A reactive dictionary that tracks dependencies on individual keys and the overall key set.
    /// </summary>
    /// <typeparam name="TKey">Type of the key. </typeparam>
    /// <typeparam name="TValue">Type of the value. </typeparam>
    public class ReactiveDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReactive
    {
        // inner storage for actual data and key-specific dependencies
        private readonly Dictionary<TKey, (TValue, Dependency)> _innerDictionary =
            new Dictionary<TKey, (TValue, Dependency)>();

        // dependency to track changes to the entire set of keys
        private readonly Dependency _keySetDependency = new Dependency();

        private bool _isDeep = false;

        public const string KeySetPropertyName = "KeySet[]";

        private TValue Deep(TValue value)
        {
            if (!_isDeep) return value;
            if (value is IReactive r)
            {
                r.SetDeep();
                return value;
            }
            var reactiveValue = Reactive.TryCreate(value);
            if (reactiveValue is IReactive rNew)
            {
                rNew.SetDeep();
                return (TValue)rNew;
            }
            return value;
        }

        public bool SetDeep()
        {
            if (_isDeep)
                return false;

            _isDeep = true;
            foreach (var pair in _innerDictionary)
            {
                var deepValue = Deep(pair.Value.Item1);
                if (!ReferenceEquals(deepValue, pair.Value.Item1))
                {
                    _innerDictionary[pair.Key] = pair.Value;
                }
            }
            return true;
        }

        public void TrackDeep()
        {
            _keySetDependency.Track();
            foreach (var pair in _innerDictionary)
            {
                var data = pair.Value;
                if (data.Item1 is IReactive r)
                {
                    r.TrackDeep();
                }
                data.Item2.Track();
            }
        }

        // readonly wrapper to intercept access
        public ICollection<TKey> Keys => new KeyCollection(this);

        public ICollection<TValue> Values => new ValueCollection(this);

        public int Count => _innerDictionary.Count;

        public bool IsReadOnly => false;

        public TValue this[TKey key]
        {
            get
            {
                // check if the key exists
                if (_innerDictionary.TryGetValue(key, out (TValue, Dependency) data))
                {
                    // if it exists, track its dependency
                    data.Item2.Track();
                    return data.Item1;
                }
                else
                {
                    // if it doesn't exist, track the KeySet dependency
                    _keySetDependency.Track();
                    // do the same as Dictionary's behavior
                    throw new KeyNotFoundException($"The given key '{key}' was not present in the dictionary.");
                }
            }
            set
            {
                // check if the key already exists
                if (_innerDictionary.ContainsKey(key))
                {
                    // if it exists, update the value and trigger its dependency
                    var data = _innerDictionary[key];
                    data.Item1 = Deep(value);
                    _innerDictionary[key] = data;
                    data.Item2.Trigger();
                }
                else
                {
                    // otherwise, add a new entry with a new Dependency
                    _innerDictionary[key] = (Deep(value), new Dependency());
                    _keySetDependency.Trigger();
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            // add the key-value pair with its dependency to the inner dictionary
            _innerDictionary.Add(key, (Deep(value), new Dependency()));

            // trigger the KeySet dependency to indicate a new key has been added
            _keySetDependency.Trigger();
        }

        public bool ContainsKey(TKey key)
        {
            if (_innerDictionary.TryGetValue(key, out var data))
            {
                // if Key exists, additionally track this Key itself
                data.Item2.Track();
                return true;
            }
            else
            {
                // otherwise, track the KeySet
                _keySetDependency.Track();
                return false;
            }
        }

        public bool Remove(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            // check if the key exists and remove it
            if (_innerDictionary.TryGetValue(key, out var data))
            {
                // if existed, remove it
                _innerDictionary.Remove(key);

                // trigger its Dependency
                data.Item2.Trigger();

                // trigger KeySet change
                _keySetDependency.Trigger();

                return true;
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_innerDictionary.TryGetValue(key, out var data))
            {
                // if Key exists, track this Key itself
                data.Item2.Track();
                // return the value
                value = data.Item1;
                return true;
            }
            else
            {
                // otherwise, track the KeySet
                _keySetDependency.Track();
                // set default value
                value = default;
                return false;
            }
        }

        public void Clear()
        {
            if (_innerDictionary.Count == 0)
                return;

            // collect dependencies to trigger before clearing
            var dependenciesToTrigger = new HashSet<Dependency>(
                _innerDictionary.Values.Select(data => data.Item2));

            // clear the inner dictionary and key dependencies
            _innerDictionary.Clear();

            // trigger dependencies for all existing keys before clearing
            foreach (var dependency in dependenciesToTrigger)
            {
                dependency.Trigger();
            }

            // trigger KeySet change
            _keySetDependency.Trigger();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            // similar to ContainsKey
            if (_innerDictionary.TryGetValue(item.Key, out var data))
            {
                // if Key exists, track this Key itself
                data.Item2.Track();
                // check value equality
                return EqualityComparer<TValue>.Default.Equals(data.Item1, item.Value);
            }
            else
            {
                // otherwise, track the KeySet
                _keySetDependency.Track();
                return false;
            }
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            // KeySet is accessed, track it
            _keySetDependency.Track();
            ((ICollection<KeyValuePair<TKey, TValue>>)_innerDictionary).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            // delegate to Remove by key, which handles dependencies
            return Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            // when enumerating, track KeySet
            _keySetDependency.Track();
            foreach (var pair in _innerDictionary)
            {
                // when yielding each pair, track its Key dependency
                pair.Value.Item2.Track();
                yield return new KeyValuePair<TKey, TValue>(pair.Key, pair.Value.Item1);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // wrapper for Keys collection to intercept access
        private class KeyCollection : ICollection<TKey>
        {
            private readonly ReactiveDictionary<TKey, TValue> _parent;

            public KeyCollection(ReactiveDictionary<TKey, TValue> parent)
            {
                _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            }

            public int Count => _parent.Count;

            public bool IsReadOnly => true;

            public bool Contains(TKey item)
            {
                if (_parent._innerDictionary.TryGetValue(item, out var data))
                {
                    // if Key exists, track this Key itself
                    data.Item2.Track();
                    return true;
                }
                else
                {
                    // otherwise, track the KeySet
                    _parent._keySetDependency.Track();
                    return false;
                }
            }

            public void CopyTo(TKey[] array, int arrayIndex)
            {
                // accessing Keys, track KeySet
                _parent._keySetDependency.Track();
                _parent._innerDictionary.Keys.CopyTo(array, arrayIndex);
            }

            public IEnumerator<TKey> GetEnumerator()
            {
                // for enumerating Keys, track KeySet
                _parent._keySetDependency.Track();
                foreach (var key in _parent._innerDictionary.Keys)
                {
                    yield return key;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            // the Keys collection is read-only; mutating methods throw NotSupportedException
            public void Add(TKey item) => throw new NotSupportedException("Mutating the Keys collection is not supported.");
            public void Clear() => throw new NotSupportedException("Mutating the Keys collection is not supported.");
            public bool Remove(TKey item) => throw new NotSupportedException("Mutating the Keys collection is not supported.");
        }

        // wrapper for Values collection to intercept access
        private class ValueCollection : ICollection<TValue>
        {
            private readonly ReactiveDictionary<TKey, TValue> _parent;
            public ValueCollection(ReactiveDictionary<TKey, TValue> parent)
            {
                _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            }
            public int Count => _parent.Count;
            public bool IsReadOnly => true;
            public bool Contains(TValue item)
            {
                // accessing Values, track KeySet and each individual key
                foreach (var data in _parent._innerDictionary.Values)
                {
                    if (EqualityComparer<TValue>.Default.Equals(data.Item1, item))
                    {
                        data.Item2.Track();
                        return true;
                    }
                }
                _parent._keySetDependency.Track();
                foreach (var data in _parent._innerDictionary.Values)
                {
                    data.Item2.Track();
                }
                return false;
            }
            public void CopyTo(TValue[] array, int arrayIndex)
            {
                // accessing Values, track KeySet and each individual key
                _parent._keySetDependency.Track();
                int i = arrayIndex;
                foreach (var data in _parent._innerDictionary.Values)
                {
                    data.Item2.Track();
                    array[i++] = data.Item1;
                }
            }
            public IEnumerator<TValue> GetEnumerator()
            {
                // accessing Values, track KeySet and each individual key
                _parent._keySetDependency.Track();
                foreach (var data in _parent._innerDictionary.Values)
                {
                    data.Item2.Track();
                    yield return data.Item1;
                }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            // the Values collection is read-only; mutating methods throw NotSupportedException
            public void Add(TValue item) => throw new NotSupportedException("Mutating the Values collection is not supported.");
            public void Clear() => throw new NotSupportedException("Mutating the Values collection is not supported.");
            public bool Remove(TValue item) => throw new NotSupportedException("Mutating the Values collection is not supported.");
        }
    }
}