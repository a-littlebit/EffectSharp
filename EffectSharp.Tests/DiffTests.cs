using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class DiffTests
    {
        private static readonly Random _random = new Random(42);

        private List<int> GenerateRandomList(int minLength, int maxLength, int minValue, int maxValue, bool unique = true)
        {
            int length = _random.Next(minLength, maxLength + 1);
            var result = new List<int>();
            var existing = unique ? new HashSet<int>() : null;
            while (result.Count < length)
            {
                int value = _random.Next(minValue, maxValue + 1);
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

        private static void AssignPrefixAndSuffix<T>(IList<T> source, IList<T> target, int prefixLength, int suffixLength)
        {
            for (int i = 0; i < prefixLength; i++)
            {
                target[i] = source[i];
            }
            for (int i = 0; i < suffixLength; i++)
            {
                target[target.Count - suffixLength + i] = source[source.Count - suffixLength + i];
            }
        }

        [Fact]
        public async Task DiffAndBindToCollection_WhenKeyed_WorksCorrectly()
        {
            var reactiveList = Reactive.Collection<(int, string)>();
            var sourceList = Reactive.Ref(GenerateRandomList(100, 200, 0, 200).Select(i => (i, i.ToString())).ToList());
            // Initial binding
            Reactive.BindTo(sourceList, reactiveList, item => item.Item1);
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list
            sourceList.Value = GenerateRandomList(100, 200, 0, 200).Select(i => (i, i.ToString())).ToList();
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list again
            var newSourceList = GenerateRandomList(50, 100, 0, 100).Select(i => (i, i.ToString())).ToList();
            AssignPrefixAndSuffix(reactiveList, newSourceList, 10, 10);
            sourceList.Value = newSourceList;
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
        }

        [Fact]
        public async Task DiffAndBindToCollection_WhenUnkeyed_WorksCorrectly()
        {
            var reactiveList = Reactive.Collection<int>();
            var sourceList = Reactive.Ref(GenerateRandomList(100, 200, 0, 100, false));
            // Initial binding
            Reactive.BindTo(sourceList, reactiveList);
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list
            sourceList.Value = GenerateRandomList(100, 200, 0, 100, false);
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
            // Update source list again
            var newSourceList = GenerateRandomList(50, 100, 0, 50, false);
            AssignPrefixAndSuffix(reactiveList, newSourceList, 10, 10);
            sourceList.Value = newSourceList;
            await Reactive.NextTick();
            Assert.Equal(sourceList.Value, reactiveList);
        }
    }
}
