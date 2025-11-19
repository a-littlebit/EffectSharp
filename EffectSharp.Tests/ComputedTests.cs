using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class ComputedTests
    {
        [Fact]
        public void Computed_WhenDependentPropertyChanges_RecomputesValue()
        {
            var product = Reactive.Create(new Product
            {
                Name = "Tablet",
                Price = 300
            });
            var computedPriceWithTax = Reactive.Computed(() => product.Price + (int)(product.Price * 0.1));
            Assert.Equal(330, computedPriceWithTax.Value);
            string? notifiedProperty = null;
            computedPriceWithTax.PropertyChanged += (s, e) =>
            {
                notifiedProperty = e.PropertyName;
            };
            product.Price = 400;
            DependencyTracker.FlushNotifyQueue();
            Assert.Equal(440, computedPriceWithTax.Value);
            Assert.Equal(nameof(computedPriceWithTax.Value), notifiedProperty);
        }

        [Fact]
        public void Computed_WhenDependentComputedChanges_RecomputesValue()
        {
            var product = Reactive.Create(new Product
            {
                Name = "Headphones",
                Price = 150
            });
            var computedDiscountedPrice = Reactive.Computed(() => product.Price - 20);
            var computedFinalPrice = Reactive.Computed(() => computedDiscountedPrice.Value + (int)(computedDiscountedPrice.Value * 0.1));
            Assert.Equal(143, computedFinalPrice.Value);
            product.Price = 200;
            Assert.Equal(198, computedFinalPrice.Value);
        }

        [Fact]
        public void Computed_WhenAccessedMultipleTimes_UsesCache()
        {
            var product = Reactive.Create(new Product
            {
                Name = "Monitor",
                Price = 200
            });
            int computeCount = 0;
            var computedPriceWithTax = Reactive.Computed(() =>
            {
                computeCount++;
                return product.Price + (int)(product.Price * 0.1);
            });
            var firstAccess = computedPriceWithTax.Value;
            var secondAccess = computedPriceWithTax.Value;
            Assert.Equal(220, firstAccess);
            Assert.Equal(220, secondAccess);
            Assert.Equal(1, computeCount); // Ensure computation happened only once
        }
    }
}
