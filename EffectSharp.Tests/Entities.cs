using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public interface Product
    {
        public string Name { get; set; }
        public int Price { get; set; }
    }

    public interface Order
    {
        [ReactiveProperty(deep: true)]
        public Product Product { get; set; }

        [ReactiveProperty(defaultValue: 1)]
        public int Quantity { get; set; }
    }

    public class ProductEntity : Product
    {
        public string Name { get; set; } = string.Empty;
        public int Price { get; set; }
    }
}
