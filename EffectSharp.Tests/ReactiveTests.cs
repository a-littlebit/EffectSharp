using System.ComponentModel;

namespace EffectSharp.Tests
{
    public class ReactiveTests
    {
        [Fact]
        public async Task Reactive_WhenSetProperty_NotifyPropertyChange()
        {
            var product = Reactive.Create<Product>();
            product.Name = "Laptop";
            product.Price = 1000;

            object? notifier = null;
            string? changedPropertyName = null;
            ((INotifyPropertyChanged)product)!.PropertyChanged += (sender, e) =>
            {
                notifier = sender;
                changedPropertyName = e.PropertyName;
            };
            product.Price = 1200;
            // Wait for the next tick to ensure the PropertyChanged event is raised
            await TaskManager.FlushNotificationQueue();
            Assert.Same(product, notifier);
            Assert.Equal(nameof(product.Price), changedPropertyName);
        }

        [Fact]
        public async Task Reactive_WhenSetNestedProperty_NotifyPropertyChange()
        {
            var order = Reactive.Create<Order>();
            order.Quantity = 2;
            order.Product.Name = "Phone";
            order.Product.Price = 500;

            object? notifier = null;
            string? changedPropertyName = null;
            ((INotifyPropertyChanged)order.Product)!.PropertyChanged += (sender, e) =>
            {
                notifier = sender;
                changedPropertyName = e.PropertyName;
            };
            order.Product.Price = 600;
            // Flush the notify queue to ensure the PropertyChanged event is raised
            await TaskManager.FlushNotificationQueue();
            Assert.Same(order.Product, notifier);
            Assert.Equal(nameof(order.Product.Price), changedPropertyName);
        }

        [Fact]
        public async Task Reactive_WhenSetProperty_TriggerSubcribers()
        {
            var product = Reactive.Create<Product>();
            product.Name = "Laptop";
            product.Price = 1000;

            bool priceChanged = false;
            Reactive.Watch(() => product.Price, (_, _) =>
            {
                priceChanged = true;
            });
            product.Price = 1200;
            await Reactive.NextTick();
            Assert.True(priceChanged);
        }
    }
}