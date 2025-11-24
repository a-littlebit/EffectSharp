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
        public void Watch_WhenRefValueChanged_TriggersEffect()
        {
            // Arrange
            var countRef = Reactive.Ref(0);
            int effectRunCount = 0;
            // Act
            IDisposable sub = Reactive.Watch(countRef, () =>
            {
                _ = countRef.Value;
                effectRunCount++;
            });
            // Change the ref value
            countRef.Value = 1;
            // Assert
            Assert.Equal(1, effectRunCount);
            // Change the ref value again
            countRef.Value = 2;
            // Assert
            Assert.Equal(2, effectRunCount);
            // Cleanup
            sub.Dispose();
            // Change the ref value after unsubscribing
            countRef.Value = 3;
            // Assert that effectRunCount did not increase
            Assert.Equal(2, effectRunCount);
        }

        [Fact]
        public void Watch_WhenWatchMultipleVariables_TriggersEffectOnAnyChange()
        {
            // Arrange
            var refA = Reactive.Ref(1);
            var refB = Reactive.Ref(10);
            int effectRunCount = 0;
            // Act
            IDisposable sub = Reactive.Watch(() => (refA.Value, refB.Value), () =>
            {
                effectRunCount++;
            });
            // Change refA
            refA.Value = 2;
            // Assert
            Assert.Equal(1, effectRunCount);
            // Change refB
            refB.Value = 20;
            // Assert
            Assert.Equal(2, effectRunCount);
            // Cleanup
            sub.Dispose();
        }

        [Fact]
        public void Watch_WhenCallback_ValueCorrect()
        {
            // Arrange
            var countRef = Reactive.Ref(10);
            int observedNewValue = 0;
            int observedOldValue = 0;
            // Act
            IDisposable sub = Reactive.Watch(countRef, (newValue, oldValue) =>
            {
                observedNewValue = newValue;
                observedOldValue = oldValue;
            });
            // Change the ref value
            countRef.Value = 20;
            // Assert
            Assert.Equal(20, observedNewValue);
            Assert.Equal(10, observedOldValue);
            // Change the ref value again
            countRef.Value = 30;
            // Assert
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
            IDisposable sub = Reactive.Watch(countRef, (newValue, oldValue) =>
            {
                observedNewValue = newValue;
                observedOldValue = oldValue;
                callbackCalled = true;
            }, new WatchOptions { Immediate = true });
            // Assert that the callback was called immediately
            Assert.True(callbackCalled);
            // Assert the values passed to the callback
            Assert.Equal(5, observedNewValue);
            Assert.Equal(5, observedOldValue);
            // Cleanup
            sub.Dispose();
        }

        [Fact]
        public void Watch_WhenDeepOption_TracksNestedChanges()
        {
            // Arrange
            var nestedRef = Reactive.Ref(new Order
            {
                Product = new Product { Name = "Widget", Price = 100 },
                Quantity = 1
            }, true);
            int effectRunCount = 0;
            // Act
            IDisposable sub = Reactive.Watch(nestedRef, () =>
            {
                _ = nestedRef.Value.Product.Price;
                effectRunCount++;
            }, new WatchOptions { Deep = true });
            // Change a nested property
            nestedRef.Value.Product.Price = 150;
            // Assert
            Assert.Equal(1, effectRunCount);
            // Cleanup
            sub.Dispose();
        }
    }
}
