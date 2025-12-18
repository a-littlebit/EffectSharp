using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class DisposeReactiveModelTests
    {
        [Fact]
        public void Generates_DisposeReactiveModel_For_Computed()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Computed]
    public int Total() => 1;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            Assert.Contains("public void DisposeReactiveModel()", text);
            Assert.Contains("this._computedTotal?.Dispose();", text);
            Assert.Contains("this._computedTotal = null;", text);
        }

        [Fact]
        public void Generates_DisposeReactiveModel_For_Watch()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _value;

    [Watch(Values = new[] { nameof(Value) })]
    public void OnChanged(int n, int o) { }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            Assert.Contains("public void DisposeReactiveModel()", text);
            Assert.Contains("this._onChanged_watchEffect?.Dispose();", text);
            Assert.Contains("this._onChanged_watchEffect = null;", text);
        }
    }
}
