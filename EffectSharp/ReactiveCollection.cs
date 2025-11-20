using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;


namespace EffectSharp
{
    /// <summary>
    /// A reactive collection that tracks dependencies on its items and count.
    /// </summary>
    /// <typeparam name="T">Type of the element. </typeparam>
    public class ReactiveCollection<T> : ObservableCollection<T>, IReactive, ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>, ICollection, IList
    {
        const string ItemsPropertyName = "Item[]";

        public ReactiveCollection() : base() { }
        public ReactiveCollection(IEnumerable<T> collection) : base(collection) { }
        public ReactiveCollection(List<T> list) : base(list) { }

        private Dependency _countDependency = new Dependency();
        private Dependency _itemsDependency = new Dependency();

        public Dependency GetDependency(string propertyName)
        {
            if (propertyName == nameof(Count))
            {
                return _countDependency;
            }
            if (propertyName == ItemsPropertyName)
            {
                return _itemsDependency;
            }
            return null;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            if (e.PropertyName == nameof(Count))
            {
                _countDependency.Trigger();
            }
            else if (e.PropertyName == ItemsPropertyName)
            {
                _itemsDependency.Trigger();
            }
        }

        public new T this[int index]
        {
            get
            {
                _itemsDependency.Track();
                return base[index];
            }
            set
            {
                base[index] = value;
                _itemsDependency.Trigger();
            }
        }

        public new int Count
        {
            get
            {
                _countDependency.Track();
                return base.Count;
            }
        }

        protected new IList<T> Items
        {
            get
            {
                _itemsDependency.Track();
                return base.Items;
            }
        }

        public new bool Contains(T item)
        {
            _itemsDependency.Track();
            return base.Contains(item);
        }

        public new void CopyTo(T[] array, int index)
        {
            _itemsDependency.Track();
            _countDependency.Track();
            base.CopyTo(array, index);
        }

        public new IEnumerator<T> GetEnumerator()
        {
            _itemsDependency.Track();
            _countDependency.Track();
            return base.GetEnumerator();

        }
        public new int IndexOf(T item)
        {
            _itemsDependency.Track();
            return base.IndexOf(item);
        }
    }
}