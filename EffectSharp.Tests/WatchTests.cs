using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class WatchTests
    {
        [Fact]
        public async Task Watch_WhenRefValueChanged_TriggersEffect()
        {
            // Arrange
            var countRef = Reactive.Ref(0);
            int effectRunCount = 0;
            // Act
            var sub = Reactive.Watch(countRef, (_, _) =>
            {
                _ = countRef.Value;
                effectRunCount++;
            });
            // Change the ref value
            countRef.Value = 1;
            // Assert
            await Reactive.NextTick();
            Assert.Equal(1, effectRunCount);
            // Change the ref value again
            countRef.Value = 2;
            // Assert
            await Reactive.NextTick();
            Assert.Equal(2, effectRunCount);
            // Cleanup
            sub.Dispose();
            // Change the ref value after unsubscribing
            countRef.Value = 3;
            // Assert that effectRunCount did not increase
            await Reactive.NextTick();
            Assert.Equal(2, effectRunCount);
        }

        [Fact]
        public async Task Watch_WhenWatchMultipleVariables_TriggersEffectOnAnyChange()
        {
            // Arrange
            var refA = Reactive.Ref(1);
            var refB = Reactive.Ref(10);
            int effectRunCount = 0;
            // Act
            var sub = Reactive.Watch(() => (refA.Value, refB.Value), (_, _) =>
            {
                effectRunCount++;
            });
            // Change refA
            refA.Value = 2;
            // Assert
            await Reactive.NextTick();
            Assert.Equal(1, effectRunCount);
            // Change refB
            refB.Value = 20;
            // Assert
            await Reactive.NextTick();
            Assert.Equal(2, effectRunCount);
            // Cleanup
            sub.Dispose();
        }

        [Fact]
        public async Task Watch_WhenCallback_ValueCorrect()
        {
            // Arrange
            var countRef = Reactive.Ref(10);
            int observedNewValue = 0;
            int observedOldValue = 0;
            // Act
            var sub = Reactive.Watch(countRef, (newValue, oldValue) =>
            {
                observedNewValue = newValue;
                observedOldValue = oldValue;
            });
            // Change the ref value
            countRef.Value = 20;
            // Assert
            await Reactive.NextTick();
            Assert.Equal(20, observedNewValue);
            Assert.Equal(10, observedOldValue);
            // Change the ref value again
            countRef.Value = 30;
            // Assert
            await Reactive.NextTick();
            Assert.Equal(30, observedNewValue);
            Assert.Equal(20, observedOldValue);
            // Cleanup
            sub.Dispose();
        }

        [Fact]
        public void Watch_WhenImmediateOption_CallbackCalledImmediately()
        {
            // Arrange
            var countRef = Reactive.Ref(5);
            int observedNewValue = 0;
            int observedOldValue = 0;
            bool callbackCalled = false;
            // Act
            var sub = Reactive.Watch(countRef, (newValue, oldValue) =>
            {
                observedNewValue = newValue;
                observedOldValue = oldValue;
                callbackCalled = true;
            }, immediate: true);
            // Assert that the callback was called immediately
            Assert.True(callbackCalled);
            // Assert the values passed to the callback
            Assert.Equal(5, observedNewValue);
            Assert.Equal(default, observedOldValue);
            // Cleanup
            sub.Dispose();
        }

        [Fact]
        public async Task Watch_WhenDeepOption_TracksNestedChanges()
        {
            // Arrange
            var order = Reactive.Create<IOrder>();
            order.Product.Name = "Widget";
            order.Product.Price = 100;
            order.Quantity = 1;

            int effectRunCount = 0;
            int productPrice = 100;
            // Act
            var sub = Reactive.Watch(() => order, (_, _) =>
            {
                effectRunCount++;
                productPrice = order.Product.Price;
            }, deep: true);
            // Change a nested property
            order.Product.Price = 150;
            // Assert
            await Reactive.NextTick();
            Assert.Equal(1, effectRunCount);
            Assert.Equal(150, productPrice);
            // Cleanup
            sub.Dispose();
        }
    }
}
