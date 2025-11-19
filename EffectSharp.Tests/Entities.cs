using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class Product
    {
        public virtual required string Name { get; set; }
        public virtual int Price { get; set; }
    }

    public class Order
    {
        public virtual required Product Product { get; set; }
        public virtual int Quantity { get; set; }
    }
}
