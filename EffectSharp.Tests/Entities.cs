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
        [ReactivePropertyAttribute(deep: true)]
        public Product Product { get; set; }

        [ReactivePropertyAttribute(defaultValue: 1)]
        public int Quantity { get; set; }
    }
}
