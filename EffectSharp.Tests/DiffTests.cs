using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class DiffTests
    {
        [Fact]
        public async Task DiffAndBindToCollection_WhenKeyed_WorksCorrectly()
        {
            var reactiveList = Reactive.Collection<(int, string)>();
            var sourceList = Reactive.Ref(new List<(int, string)> { (1, "1"), (2, "2"), (3, "3") });
            // Initial binding
            Reactive.DiffAndBindTo(sourceList, reactiveList, item => item.Item1);
            await Reactive.NextTick();
            Assert.Equal([(1, "1"), (2, "2"), (3, "3")], reactiveList);
            // Update source list
            sourceList.Value = [(2, "2"), (3, "3"), (4, "4")];
            await Reactive.NextTick();
            Assert.Equal([(2, "2"), (3, "3"), (4, "4")], reactiveList);
            // Update source list again
            sourceList.Value = [(5, "5"), (4, "4")];
            await Reactive.NextTick();
            Assert.Equal([(5, "5"), (4, "4")], reactiveList);
        }

        [Fact]
        public async Task DiffAndBindToCollection_WhenUnkeyed_WorksCorrectly()
        {
            var reactiveList = Reactive.Collection<string>();
            var sourceList = Reactive.Ref(new List<string> { "A", "B", "C" });
            // Initial binding
            Reactive.DiffAndBindTo(sourceList, reactiveList);
            await Reactive.NextTick();
            Assert.Equal(["A", "B", "C"], reactiveList);
            // Update source list
            sourceList.Value = ["B", "C", "D"];
            await Reactive.NextTick();
            Assert.Equal(["B", "C", "D"], reactiveList);
            // Update source list again
            sourceList.Value = ["E", "D"];
            await Reactive.NextTick();
            Assert.Equal(["E", "D"], reactiveList);
        }
    }
}
