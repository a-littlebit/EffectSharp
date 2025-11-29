using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class ReactiveCollectionTests
    {
        [Fact]
        public async Task ReactiveCollection_WhenCountChanged_TrackDependency()
        {
            var reactiveList = Reactive.Collection<int>();
            var listCount = Reactive.Computed(() => reactiveList.Count);

            Assert.Equal(0, listCount.Value);

            bool countChanged = false;
            Reactive.Watch(() => reactiveList.Count, (_, _) => countChanged = true);

            reactiveList.Add(1);
            await Reactive.NextTick();
            Assert.True(countChanged);
            Assert.Equal(1, listCount.Value);
        }

        [Fact]
        public async Task ReactiveCollection_WhenItemAccessed_TrackDependency()
        {
            var reactiveList = Reactive.Collection([10, 20, 30]);
            var firstItem = Reactive.Computed(() => reactiveList[0]);
            Assert.Equal(10, firstItem.Value);
            bool itemChanged = false;
            Reactive.Watch(() => reactiveList[0], (_, _) => itemChanged = true);
            reactiveList[0] = 100;
            await Reactive.NextTick();
            Assert.True(itemChanged);
            Assert.Equal(100, firstItem.Value);
        }

        [Fact]
        public async Task ReactiveCollection_WhenItemAdded_NotifySubscribers()
        {
            var reactiveList = Reactive.Collection<int>();
            var sum = Reactive.Computed(() => reactiveList.Sum());
            Assert.Equal(0, sum.Value);
            bool sumChanged = false;
            Reactive.Watch(() => reactiveList.Sum(), (_, _) => sumChanged = true);
            reactiveList.Add(5);
            await Reactive.NextTick();
            Assert.True(sumChanged);
            Assert.Equal(5, sum.Value);
        }

        [Fact]
        public async Task ReactiveCollection_WhenItemRemoved_NotifySubscribers()
        {
            var reactiveList = Reactive.Collection([1, 2, 3]);
            var sum = Reactive.Computed(() => reactiveList.Sum());
            Assert.Equal(6, sum.Value);
            bool sumChanged = false;
            Reactive.Watch(() => reactiveList.Sum(), (_, _) => sumChanged = true);
            reactiveList.Remove(2);
            await Reactive.NextTick();
            Assert.True(sumChanged);
            Assert.Equal(4, sum.Value);
        }

        [Fact]
        public async Task ReactiveCollection_WhenCleared_NotifySubscribers()
        {
            var reactiveList = Reactive.Collection([1, 2, 3]);
            var count = Reactive.Computed(() => reactiveList.Count);
            Assert.Equal(3, count.Value);
            bool countChanged = false;
            Reactive.Watch(() => reactiveList.Count, (_, _) => countChanged = true);
            reactiveList.Clear();
            await Reactive.NextTick();
            Assert.True(countChanged);
            Assert.Equal(0, count.Value);
        }
    }
}
