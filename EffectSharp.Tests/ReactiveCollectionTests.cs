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
        public void ReactiveList_WhenCountChanged_TrackDependency()
        {
            var reactiveList = Reactive.Collection<int>();
            var listCount = Reactive.Computed(() => reactiveList.Count);

            Assert.Equal(0, listCount.Value);

            bool countChanged = false;
            reactiveList.GetDependency("Count")!.AddSubscriber(new Effect(() => countChanged = true));

            reactiveList.Add(1);
            Assert.True(countChanged);
            Assert.Equal(1, listCount.Value);
        }

        [Fact]
        public void ReactiveList_WhenItemAccessed_TrackDependency()
        {
            var reactiveList = Reactive.Collection([10, 20, 30]);
            var firstItem = Reactive.Computed(() => reactiveList[0]);
            Assert.Equal(10, firstItem.Value);
            bool itemChanged = false;
            reactiveList.GetDependency("Item[]")!.AddSubscriber(new Effect(() => itemChanged = true));
            reactiveList[0] = 100;
            Assert.True(itemChanged);
            Assert.Equal(100, firstItem.Value);
        }

        [Fact]
        public void ReactiveList_WhenItemAdded_NotifySubscribers()
        {
            var reactiveList = Reactive.Collection<int>();
            var sum = Reactive.Computed(() => reactiveList.Sum());
            Assert.Equal(0, sum.Value);
            bool sumChanged = false;
            reactiveList.GetDependency("Item[]")!.AddSubscriber(new Effect(() => sumChanged = true));
            reactiveList.Add(5);
            Assert.True(sumChanged);
            Assert.Equal(5, sum.Value);
        }

        [Fact]
        public void ReactiveList_WhenItemRemoved_NotifySubscribers()
        {
            var reactiveList = Reactive.Collection([1, 2, 3]);
            var sum = Reactive.Computed(() => reactiveList.Sum());
            Assert.Equal(6, sum.Value);
            bool sumChanged = false;
            reactiveList.GetDependency("Item[]")!.AddSubscriber(new Effect(() => sumChanged = true));
            reactiveList.Remove(2);
            Assert.True(sumChanged);
            Assert.Equal(4, sum.Value);
        }

        [Fact]
        public void ReactiveList_WhenCleared_NotifySubscribers()
        {
            var reactiveList = Reactive.Collection([1, 2, 3]);
            var count = Reactive.Computed(() => reactiveList.Count);
            Assert.Equal(3, count.Value);
            bool countChanged = false;
            reactiveList.GetDependency("Count")!.AddSubscriber(new Effect(() => countChanged = true));
            reactiveList.Clear();
            Assert.True(countChanged);
            Assert.Equal(0, count.Value);
        }

        [Fact]
        public void DiffAndBindToCollection_WorksCorrectly()
        {
            var reactiveList = Reactive.Collection<(int, string)>();
            var sourceList = Reactive.Ref(new List<(int, string)> { (1, "1"), (2, "2"), (3, "3") });
            // Initial binding
            Reactive.DiffAndBindTo(sourceList, reactiveList);
            Assert.Equal([(1, "1"), (2, "2"), (3, "3")], reactiveList);
            // Update source list
            sourceList.Value = [(2, "2"), (3, "updated"), (4, "4")];
            Assert.Equal([(2, "2"), (3, "updated"), (4, "4")], reactiveList);
            // Update source list again
            sourceList.Value = [(4, "4"), (5, "5")];
            Assert.Equal([(4, "4"), (5, "5")], reactiveList);
        }
    }
}
