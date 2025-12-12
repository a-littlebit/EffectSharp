using System.ComponentModel;

namespace EffectSharp.Tests
{
    public class ReactiveProxyTests
    {
        [Fact]
        public async Task Reactive_WhenSetProperty_NotifyPropertyChange()
        {
            var product = Reactive.Create<IProduct>(new Product()
            {
                Name = "Initial",
                Price = 500
            });

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
            Assert.Equal(1200, product.Price);
        }

        [Fact]
        public async Task Reactive_WhenSetNestedProperty_NotifyPropertyChange()
        {
            var order = Reactive.Create<IOrder>();
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
            // Flush the notification queue to ensure the PropertyChanged event is raised
            await TaskManager.FlushNotificationQueue();
            Assert.Same(order.Product, notifier);
            Assert.Equal(nameof(order.Product.Price), changedPropertyName);
            Assert.Equal(600, order.Product.Price);
        }

        [Fact]
        public async Task Reactive_WhenSetProperty_TriggerSubcribers()
        {
            var product = Reactive.Create<IProduct>();
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

        [Fact]
        public async Task Reactive_WhenSetNonReactiveProperty_DoesNotTriggerOrNotifyChange()
        {
            var entity = Reactive.Create<IReactiveEntity>();
            entity.Id = 1;
            bool observedChange = false;
            object? notifier = null;
            Reactive.Watch(() => entity.Id, (_, _) =>
            {
                observedChange = true;
            });
            ((INotifyPropertyChanged)entity)!.PropertyChanged += (sender, e) =>
            {
                notifier = sender;
            };
            entity.Id = 2;
            // Wait for the next tick to ensure all effects and notifications are processed
            await Reactive.NextTick();
            Assert.Null(notifier);
            Assert.False(observedChange);
            Assert.Equal(2, entity.Id);
        }

        [Fact]
        public async Task Reactive_WhenSetPropertyWithCustomEqualityComparer_DetectsChangesCorrectly()
        {
            var entity = Reactive.Create<IReactiveEntity>();
            entity.Name = "Initial Name";
            entity.CreatedAt = DateTime.UtcNow;
            int nameChangeCount = 0;
            int createdAtChangeCount = 0;
            ((INotifyPropertyChanged)entity).PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(entity.Name))
                {
                    nameChangeCount++;
                }
                else if (e.PropertyName == nameof(entity.CreatedAt))
                {
                    createdAtChangeCount++;
                }
            };
            // Setting the same name should trigger change due to NeverEqualComparer
            var sameName = entity.Name;
            entity.Name = sameName;
            await Reactive.NextTick();
            Assert.Equal(1, nameChangeCount);
            // Setting a different CreatedAt should not trigger change due to FixedResultComparer
            entity.CreatedAt = entity.CreatedAt.AddMinutes(10);
            await Reactive.NextTick();
            Assert.Equal(0, createdAtChangeCount);
        }
    }
}