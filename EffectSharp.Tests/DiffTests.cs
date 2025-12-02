using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class DiffTests
    {
        List<int> GenerateRandomList(int minLength, int maxLength, int minValue, int maxValue, bool unique = true)
        {
            var rand = new Random();
            int length = rand.Next(minLength, maxLength + 1);
            var result = new List<int>();
            var existing = unique ? new HashSet<int>() : null;
            while (result.Count < length)
            {
                int value = rand.Next(minValue, maxValue + 1);
                if (unique)
                {
                    if (existing!.Add(value))
                    {
                        result.Add(value);
                    }
                }
                else
                {
                    result.Add(value);
                }
            }
            return result;
        }

        [Fact]
        public async Task DiffAndBindToCollection_WhenKeyed_WorksCorrectly()
        {
            var reactiveList = Reactive.Collection<(int, string)>();
            var sourceList = Reactive.Ref(GenerateRandomList(100, 200, 0, 200).Select(i => (i, i.ToString())).ToList());
            // Initial binding
            Reactive.DiffAndBindTo(sourceList, reactiveList, item => item.Item1);
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list
            sourceList.Value = GenerateRandomList(100, 200, 0, 200).Select(i => (i, i.ToString())).ToList();
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list again
            sourceList.Value = GenerateRandomList(50, 100, 0, 100).Select(i => (i, i.ToString())).ToList();
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
        }

        [Fact]
        public async Task DiffAndBindToCollection_WhenUnkeyed_WorksCorrectly()
        {
            var reactiveList = Reactive.Collection<int>();
            var sourceList = Reactive.Ref(GenerateRandomList(100, 200, 0, 100, false));
            // Initial binding
            Reactive.DiffAndBindTo(sourceList, reactiveList);
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list
            sourceList.Value = GenerateRandomList(100, 200, 0, 100, false);
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list again
            sourceList.Value = GenerateRandomList(50, 100, 0, 50, false);
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
        }
    }
}
