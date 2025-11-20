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
        public void ReactiveDictionary_WhenValueChanged_TriggerSpecificKeyChange()
        {
            var reactiveDict = Reactive.Dictionary<string, int>();
            reactiveDict["a"] = 1;
            var computedValue = Reactive.Computed(() => reactiveDict["a"] * 2);
            Assert.Equal(2, computedValue.Value);

            bool valueChanged = false;
            bool countChanged = false;
            reactiveDict.GetDependency("a")!.AddSubscriber(new Effect(() => valueChanged = true));
            Reactive.Watch(() => reactiveDict.Count, () => countChanged = true);

            reactiveDict["a"] = 3;
            Assert.True(valueChanged);
            Assert.False(countChanged);
            Assert.Equal(6, computedValue.Value);
        }

        [Fact]
        public void ReactiveDictionary_WhenUnexistentKeyAccessed_TrackKeySetDependency()
        {
            var reactiveDict = Reactive.Dictionary<string, int>();
            var hasBKey = Reactive.Computed(() => reactiveDict.ContainsKey("b"));
            Assert.False(hasBKey.Value);
            bool keySetChanged = false;
            reactiveDict.GetDependency(ReactiveDictionary<string, int>.KeySetPropertyName)!
                .AddSubscriber(new Effect(() => keySetChanged = true));
            reactiveDict["b"] = 2;
            Assert.True(keySetChanged);
            Assert.True(hasBKey.Value);
        }
    }
}
