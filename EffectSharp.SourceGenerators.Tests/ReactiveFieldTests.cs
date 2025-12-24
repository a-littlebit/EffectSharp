using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class ReactiveFieldTests
    {

        [Fact]
        public void Generates_ReactiveField_Property_Dependency_And_Notification()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _count;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("public int Count", text);
            Assert.Contains("_count_dependency.Track();", text);
            Assert.Contains("EqualityComparer<int>.Default.Equals(_count, value)", text);
            Assert.Contains("PropertyChanging?.Invoke(this,", text);
            Assert.Contains("_count = value;", text);
            Assert.Contains("TaskManager.QueueNotification(this, nameof(Count)", text);
            Assert.Contains("PropertyChanged?.Invoke(this,", text);
        }

        [Fact]
        public void Generates_EqualsMethod_Custom_For_ReactiveField()
        {
            var src = @"
using EffectSharp.SourceGenerators;

static class Cmp { public static bool AreEqual(int a, int b) => a == b; }

[ReactiveModel]
public partial class Sample
{
    [ReactiveField(EqualsMethod = ""Cmp.AreEqual"")]
    private int _x;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("if (Cmp.AreEqual(_x, value))", text);
        }

        [Fact]
        public void Generates_ReactiveField_For_IAtomic_Value_Access()
        {
            var src = @"
using EffectSharp.SourceGenerators;
using EffectSharp;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private AtomicInt _x;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("return _x.Value;", text);
            Assert.Contains("_x.Value = value;", text);
        }

        [Fact]
        public void Generates_TrackDeep_For_IReactive_And_ReferenceType()
        {
            var src = @"
using EffectSharp.SourceGenerators;
using EffectSharp;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField] private IReactive _rx;
    [ReactiveField] private object _obj;
    [ReactiveField] private string _str;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("_rx_dependency.Track();", text);
            Assert.Contains("_obj_dependency.Track();", text);
            Assert.Contains("_str_dependency.Track();", text);
            Assert.Contains("_rx?.TrackDeep();", text);
            Assert.Contains("is IReactive r__obj", text);
            Assert.Contains("r__obj.TrackDeep();", text);
            Assert.DoesNotContain("is string r__str", text);
        }
    }
}
