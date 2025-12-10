using System;
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
}
