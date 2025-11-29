using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class ReactiveDictionaryTests
    {
        [Fact]
        public async Task ReactiveDictionary_WhenValueChanged_TriggerSpecificKeyChange()
        {
            var reactiveDict = Reactive.Dictionary<string, int>();
            reactiveDict["a"] = 1;
            var computedValue = Reactive.Computed(() => reactiveDict["a"] * 2);
            Assert.Equal(2, computedValue.Value);

            bool valueChanged = false;
            bool countChanged = false;
            Reactive.Watch(() => reactiveDict["a"], (_, _) => valueChanged = true);
            Reactive.Watch(() => reactiveDict.Count, (_, _) => countChanged = true);

            reactiveDict["a"] = 3;
            await Reactive.NextTick();
            Assert.True(valueChanged);
            Assert.False(countChanged);
            Assert.Equal(6, computedValue.Value);
        }

        [Fact]
        public async Task ReactiveDictionary_WhenUnexistentKeyAccessed_TrackKeySetDependency()
        {
            var reactiveDict = Reactive.Dictionary<string, int>();
            var hasBKey = Reactive.Computed(() => reactiveDict.ContainsKey("b"));
            Assert.False(hasBKey.Value);
            bool keySetChanged = false;
            Reactive.Watch(() => reactiveDict.ContainsKey("b"), (_, _) => keySetChanged = true);
            reactiveDict["b"] = 2;
            await Reactive.NextTick();
            Assert.True(keySetChanged);
            Assert.True(hasBKey.Value);
        }
    }
}
