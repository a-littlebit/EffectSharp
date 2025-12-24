using System.Linq;
using Xunit;
using EffectSharp.SourceGenerators;
using EffectSharp;

namespace EffectSharp.SourceGenerators.Tests
{
    public class DeepTrackingTests
    {
        [Fact]
        public void Generates_TrackDeep_For_Deep_Property()
        {
            var src = @"
using EffectSharp.SourceGenerators;
using EffectSharp;

[ReactiveModel]
public partial class Sample
{
    private MyReactive _custom = new MyReactive();
    [Deep] public MyReactive Custom => _custom;
}

public class MyReactive : IReactive { public void TrackDeep() { } }
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("this.Custom?.TrackDeep();", text);
        }

        [Fact]
        public void Generates_TrackDeep_For_Maybe_Reactive_Deep_Property()
        {
            var src = @"
using EffectSharp.SourceGenerators;
using EffectSharp;

[ReactiveModel]
public partial class Sample
{
    private object _custom = new MyReactive();
    [Deep] public object Custom => _custom;
}

public class MyReactive : IReactive { public void TrackDeep() { } }
";

            var (_, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("if (deep_Custom0 is IReactive r_deep_Custom0)", text);
            Assert.Contains("r_deep_Custom0.TrackDeep();", text);
        }

        [Fact]
        public void Reports_EFSP5001_When_Deep_Target_Not_Reactive()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Deep] public int NotReactive => 42;
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP5001");
            Assert.NotNull(diag);
        }

        [Fact]
        public void Reports_EFSP5002_When_Deep_Method_Not_Computed()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Deep]
    public string NotComputed() => ""value"";
}
";

            var (_, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP5002");
            Assert.NotNull(diag);
        }

        [Fact]
        public void Generates_TrackDeep_For_Deep_Computed_Method()
        {
            var src = @"
using EffectSharp.SourceGenerators;
using EffectSharp;

[ReactiveModel]
public partial class Sample
{
    [Deep]
    [Computed]
    public MyReactive ComputeItem() => new MyReactive();
}

public class MyReactive : IReactive { public void TrackDeep() { } }
";

            var (_, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("this.Item?.TrackDeep();", text);
        }

        [Fact]
        public void Generates_TrackDeep_For_Deep_ComputedList_Method()
        {
            var src = @"
using EffectSharp.SourceGenerators;
using EffectSharp;
using System.Collections.Generic;

[ReactiveModel]
public partial class Sample
{
    [Deep]
    [ComputedList]
    public MyReactiveList ComputeItems() => new MyReactiveList();
}

public class MyReactive : IReactive { public void TrackDeep() { } }
public class MyReactiveList : List<MyReactive>, IReactive { public void TrackDeep() { } }
";

            var (_, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("this.Items?.TrackDeep();", text);
        }
    }
}
