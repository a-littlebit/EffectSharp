using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public interface IProduct
    {
        string Name { get; set; }
        int Price { get; set; }
    }

    public interface IOrder
    {
        [ReactiveProperty(deep: true)]
        IProduct Product { get; set; }

        [ReactiveProperty(defaultValue: 1)]
        int Quantity { get; set; }
    }

    public class Product : IProduct
    {
        public string Name { get; set; } = string.Empty;
        public int Price { get; set; }
    }

    public class NeverEqualComparer : IEqualityComparer
    {
        public new bool Equals(object? x, object? y) => false;
        public int GetHashCode(object obj) => obj?.GetHashCode() ?? 0;
    }

    public class FixedResultComparer<T> : IEqualityComparer<T>
    {
        private readonly bool _result;

        public FixedResultComparer(bool result)
        {
            _result = result;
        }

        public bool Equals(T? x, T? y) => _result;
        public int GetHashCode(T obj) => obj?.GetHashCode() ?? 0;
    }

    public interface IReactiveEntity
    {
        [ReactiveProperty(reactive: false)]
        public int Id { get; set; }

        [ReactiveProperty(equalityComparer: typeof(NeverEqualComparer), defaultValue: "")]
        public string Name { get; set; }

        [ReactiveProperty(equalityComparer: typeof(FixedResultComparer<DateTime>), equalityComparerConstructorArgs: [true])]
        public DateTime CreatedAt { get; set; }
    }
}
