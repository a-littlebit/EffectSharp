using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class ComputedTests
    {
        [Fact]
        public void Generates_Computed_Property_When_Annotated()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Computed]
    public int Total() => 42;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);
            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("private Computed<int> _computedTotal;", text);
            Assert.Contains("public int ComputedTotal => _computedTotal.Value;", text);
            Assert.Contains("Reactive.Computed<int>(() => this.Total())", text);
            Assert.Contains("this._computedTotal?.Dispose();", text);
            Assert.Contains("this._computedTotal = null;", text);
        }

        [Fact]
        public void Generates_Computed_SetterMethod_When_Provided()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Computed(Setter = ""SetTotal"")]
    public int Total() => 42;
    public void SetTotal(int v) { }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("public int ComputedTotal { get => _computedTotal.Value; set => _computedTotal.Value = value; }", text);
            Assert.Contains("setter: SetTotal", text);
        }

        [Fact]
        public void Honors_Computed_Constructor_Setter_Argument()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Computed(""SetTotal"")]
    public int Total() => 42;
    public void SetTotal(int v) { }
}
";

            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("setter: SetTotal", text);
        }

        [Fact]
        public void Generates_Computed_Naming_Rules_ComputePrefix_To_Property()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Computed]
    public int ComputeTotal() => 1;

    [Computed]
    public int Total() => 2;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("public int Total =>", text);
            Assert.Contains("public int ComputedTotal =>", text);
        }

        [Fact]
        public void Generates_Computed_PropertyChanged_And_PropertyChanged_Hook()
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
            Assert.Contains("PropertyChanging += (s, e) =>", text);
            Assert.Contains("PropertyChanged += (s, e) =>", text);
            Assert.Contains("nameof(this.ComputedTotal)", text);
        }

        [Fact]
        public void Reports_EFSP3001_When_Computed_Method_Has_Parameters()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Computed]
    public int Total(int x) => x + 1;
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP3001");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Error, diag!.Severity);
        }
    }
}
