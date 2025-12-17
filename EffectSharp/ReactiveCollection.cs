using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;


namespace EffectSharp
{
    /// <summary>
    /// A reactive collection that tracks dependencies on its items and count.
    /// </summary>
    /// <typeparam name="T">Type of the element. </typeparam>
    public class ReactiveCollection<T> : ObservableCollection<T>, IReactive, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>, ICollection, IList
    {
        /// <summary>
        /// Initializes an empty reactive collection.
        /// </summary>
        public ReactiveCollection() : base() { }
        /// <summary>
        /// Initializes a reactive collection with the specified items.
        /// </summary>
        public ReactiveCollection(IEnumerable<T> collection) : base(collection) { }
        /// <summary>
        /// Initializes a reactive collection with the specified list.
        /// </summary>
        public ReactiveCollection(List<T> list) : base(list) { }

        private readonly List<Dependency> _indexDependencies = new List<Dependency>();
        private readonly Dependency _listDependency = new Dependency();

        private void EnsureDependencyIndex(int index)
        {
            int extend = index - _indexDependencies.Count + 1;
            if (extend > 0)
            {
                _indexDependencies.AddRange(Enumerable.Repeat<Dependency>(null, extend));
            }
        }

        private void TrackIndexDependency(int index)
        {
            EnsureDependencyIndex(index);
            var dep = _indexDependencies[index];
            if (dep == null)
            {
                dep = new Dependency();
                _indexDependencies[index] = dep;
            }
            dep.Track();
        }

        private void TriggerIndexDependency(int index)
        {
            if (index < _indexDependencies.Count)
            {
                var dep = _indexDependencies[index];
                dep?.Trigger();
            }
        }

        /// <summary>
        /// Gets or sets the item at the specified index, participating in index-level dependency tracking.
        /// </summary>
        public new T this[int index]
        {
            get
            {
                TrackIndexDependency(index);
                return base[index];
            }
            set
            {
                base[index] = value;
                TriggerIndexDependency(index);
                _listDependency.Trigger();
            }
        }

        /// <summary>
        /// Gets the number of elements in the collection and participates in dependency tracking.
        /// </summary>
        public new int Count
        {
            get
            {
                _listDependency.Track();
                return base.Count;
            }
        }

        /// <summary>
        /// Clears the collection and triggers all index dependencies and list dependency.
        /// </summary>
        protected override void ClearItems()
        {
            base.ClearItems();
            foreach (var dep in _indexDependencies)
            {
                dep?.Trigger();
            }
            _indexDependencies.Clear();
            _listDependency.Trigger();
        }

        /// <summary>
        /// Inserts an item and triggers affected index dependencies and list dependency.
        /// </summary>
        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            EnsureDependencyIndex(index);
            _indexDependencies.Insert(index, new Dependency());
            for (int i = index + 1; i < _indexDependencies.Count; i++)
            {
                _indexDependencies[i]?.Trigger();
            }
            _listDependency.Trigger();
        }

        /// <summary>
        /// Moves an item and triggers affected index dependencies.
        /// </summary>
        protected override void MoveItem(int oldIndex, int newIndex)
        {
            base.MoveItem(oldIndex, newIndex);
            if (oldIndex == newIndex) return;
            var min = Math.Min(oldIndex, newIndex);
            var max = Math.Max(oldIndex, newIndex);
            for (int i = min; i <= Math.Min(max, _indexDependencies.Count - 1); i++)
            {
                _indexDependencies[i]?.Trigger();
            }
        }

        /// <summary>
        /// Removes an item and triggers affected index dependencies and list dependency.
        /// </summary>
        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            for (int i = index; i < _indexDependencies.Count; i++)
            {
                _indexDependencies[i]?.Trigger();
            }
            if (index < _indexDependencies.Count)
            {
                _indexDependencies.RemoveAt(index);
            }
            _listDependency.Trigger();
        }

        /// <summary>
        /// Replaces an item and triggers the index dependency and list dependency.
        /// </summary>
        protected override void SetItem(int index, T item)
        {
            base.SetItem(index, item);
            TriggerIndexDependency(index);
            _listDependency.Trigger();
        }

        /// <summary>
        /// Determines whether the collection contains the specified item and tracks appropriate dependencies.
        /// </summary>
        public new bool Contains(T item)
        {
            int index = base.IndexOf(item);
            if (index >= 0)
            {
                TrackIndexDependency(index);
                return true;
            }
            else
            {
                _listDependency.Track();
                return false;
            }
        }

        /// <summary>
        /// Copies the elements to the specified array and tracks list dependency.
        /// </summary>
        public new void CopyTo(T[] array, int index)
        {
            _listDependency.Track();
            base.CopyTo(array, index);
        }

        /// <summary>
        /// Returns an enumerator and tracks list dependency.
        /// </summary>
        public new IEnumerator<T> GetEnumerator()
        {
            _listDependency.Track();
            return base.GetEnumerator();

        }
        /// <summary>
        /// Returns the index of the item and tracks affected index dependencies or list dependency if not found.
        /// </summary>
        public new int IndexOf(T item)
        {
            int index = base.IndexOf(item);
            if (index >= 0)
            {
                for (int i = 0; i <= Math.Min(index, _indexDependencies.Count - 1); i++)
                {
                    _indexDependencies[i]?.Track();
                }
            }
            else
            {
                _listDependency.Track();
            }
            return index;
        }

        /// <summary>
        /// Recursively tracks nested reactive elements in the collection.
        /// </summary>
        public void TrackDeep()
        {
            // _listDependency.Track(); // Tracked in foreach
            foreach (var item in this)
            {
                if (item != null && item is IReactive r)
                {
                    r.TrackDeep();
                }
            }
        }
    }
}