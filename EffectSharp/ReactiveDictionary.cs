using EffectSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace EffectSharp;

/// <summary>
/// A reactive dictionary that tracks dependencies on individual keys and the overall key set.
/// </summary>
/// <typeparam name="TKey">Type of the key.</typeparam>
/// <typeparam name="TValue">Type of the value. </typeparam>
public class ReactiveDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReactive
    where TKey : notnull
{
    // inner storage for actual data
    private readonly Dictionary<TKey, TValue> _innerDictionary = new();

    // maintain dependencies for each key
    private readonly Dictionary<TKey, Dependency> _keyDependencies = new();

    // dependency to track changes to the entire set of keys
    private readonly Dependency _keySetDependency = new Dependency();

    public const string KeySetPropertyName = "KeySet[]";

    public Dependency? GetDependency(string propertyName)
    {
        if (typeof(TKey) == typeof(string))
        {
            if (ContainsKey((TKey)(object)propertyName))
            {
                return GetOrCreateKeyDependency((TKey)(object)propertyName);
            }
            else
            {
                return _keySetDependency;
            }
        }
        else if (propertyName == KeySetPropertyName)
        {
            return _keySetDependency;
        }
        return null;
    }

    // readonly wrapper to intercept access
    public ICollection<TKey> Keys => new KeyCollection(this);

    public ICollection<TValue> Values
    {
        get
        {
            // when accessing Values, track KeySet and each individual key
            _keySetDependency.Track();
            foreach (var key in _innerDictionary.Keys)
            {
                GetOrCreateKeyDependency(key).Track();
            }
            return new ReadOnlyCollection<TValue>(new List<TValue>(_innerDictionary.Values));
        }
    }

    public int Count => _innerDictionary.Count;

    public bool IsReadOnly => false;

    public TValue this[TKey key]
    {
        get
        {
            // check if the key exists
            if (_innerDictionary.TryGetValue(key, out TValue? value))
            {
                // if it exists, track its dependency
                GetOrCreateKeyDependency(key).Track();
                return value;
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
            bool keyExists = _innerDictionary.ContainsKey(key);

            // update the value in the inner dictionary
            _innerDictionary[key] = value;

            // trigger the dependency for this specific key
            GetOrCreateKeyDependency(key).Trigger();

            // if the key is new, trigger the KeySet dependency
            if (!keyExists)
            {
                _keySetDependency.Trigger();
            }
        }
    }

    public void Add(TKey key, TValue value)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        // add the key-value pair to the inner dictionary
        _innerDictionary.Add(key, value);

        // create a new Dependency for this key (but not tracking it yet)
        _keyDependencies[key] = new Dependency();

        // trigger the KeySet dependency to indicate a new key has been added
        _keySetDependency.Trigger();
    }

    public bool ContainsKey(TKey key)
    {
        bool exists = _innerDictionary.ContainsKey(key);
        if (exists)
        {
            // if Key exists, additionally track this Key itself
            GetOrCreateKeyDependency(key).Track();
        }
        else
        {
            // otherwise, track the KeySet
            _keySetDependency.Track();
        }
        return exists;
    }

    public bool Remove(TKey key)
    {
        if (key == null)
            throw new ArgumentNullException(nameof(key));

        // check if the key exists and remove it
        if (_innerDictionary.Remove(key))
        {
            // if existed and removed, trigger its Dependency
            if (_keyDependencies.TryGetValue(key, out Dependency? dependency))
            {
                dependency.Trigger();
                _keyDependencies.Remove(key); // also remove its Dependency tracking
            }

            // trigger KeySet change
            _keySetDependency.Trigger();

            return true;
        }
        return false;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_innerDictionary.TryGetValue(key, out value))
        {
            // if Key exists, track this Key itself
            GetOrCreateKeyDependency(key).Track();
            return true;
        }
        else
        {
            // otherwise, track the KeySet
            _keySetDependency.Track();
        }
        return false;
    }

    public void Clear()
    {
        if (_innerDictionary.Count == 0)
            return;

        // clear the inner dictionary and key dependencies
        _innerDictionary.Clear();

        // trigger dependencies for all existing keys before clearing
        foreach (var dependency in _keyDependencies.Values)
        {
            dependency.Trigger();
        }
        _keyDependencies.Clear();

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
        bool exists = ((ICollection<KeyValuePair<TKey, TValue>>)_innerDictionary).Contains(item);
        if (exists)
        {
            GetOrCreateKeyDependency(item.Key).Track();
        }
        else
        {
            _keySetDependency.Track();
        }
        return exists;
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
            GetOrCreateKeyDependency(pair.Key).Track();
            yield return pair;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // utility to get or create Dependency for a specific key
    private Dependency GetOrCreateKeyDependency(TKey key)
    {
        if (!_keyDependencies.TryGetValue(key, out Dependency? dependency))
        {
            dependency = new Dependency();
            _keyDependencies[key] = dependency;
        }
        return dependency;
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
            bool exists = _parent._innerDictionary.ContainsKey(item);
            if (exists)
            {
                // if Key exists, track this Key itself
                _parent.GetOrCreateKeyDependency(item).Track();
            }
            else
            {
                // otherwise, track the KeySet
                _parent._keySetDependency.Track();
            }
            return exists;
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
}