using System.ComponentModel;

namespace EffectSharp.Tests
{
    public class ReactiveTests
    {
        [Fact]
        public async Task Reactive_WhenSetProperty_TriggerPropertyChange()
        {
            var product = Reactive.Create(new Product
            {
                Name = "Laptop",
                Price = 1000
            });
            string? changedPropertyName = null;
            ((INotifyPropertyChanged)product)!.PropertyChanged += (sender, e) =>
            {
                changedPropertyName = e.PropertyName;
            };
            product.Price = 1200;
            // Wait for the next tick to ensure the PropertyChanged event is raised
            await TaskManager.FlushNotifyQueue();
            Assert.Equal(nameof(product.Price), changedPropertyName);
        }

        [Fact]
        public async Task Reactive_WhenSetNestedProperty_TriggerPropertyChange()
        {
            var order = Reactive.CreateDeep(new Order
            {
                Product = new Product
                {
                    Name = "Phone",
                    Price = 500
                },
                Quantity = 2
            });
            string? changedPropertyName = null;
            ((INotifyPropertyChanged)order.Product)!.PropertyChanged += (sender, e) =>
            {
                changedPropertyName = e.PropertyName;
            };
            order.Product.Price = 600;
            // Flush the notify queue to ensure the PropertyChanged event is raised
            await TaskManager.FlushNotifyQueue();
            Assert.Equal(nameof(order.Product.Price), changedPropertyName);
        }

        [Fact]
        public void Reactive_WhenSetProperty_NotifySubcribers()
        {
            var product = Reactive.Create(new Product
            {
                Name = "Laptop",
                Price = 1000
            });
            string? changedPropertyName = null;
            ((IReactive)product)!.GetDependency(nameof(product.Price))?.AddSubscriber(new Effect(() =>
            {
                changedPropertyName = nameof(product.Price);
            }));
            product.Price = 1200;
            Assert.Equal(nameof(product.Price), changedPropertyName);
        }
    }
}