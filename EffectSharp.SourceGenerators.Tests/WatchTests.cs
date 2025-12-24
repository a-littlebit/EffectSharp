using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class WatchTests
    {
        [Fact]
        public void Generates_Watch_With_Options_When_Provided()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _value;

    [Watch(Values = new[] { nameof(Value) }, Immediate = true, Scheduler = ""TaskScheduler.Default"", SuppressEquality = false)]
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
            Assert.Contains("Reactive.Watch(() => Value", text);
            Assert.Contains("immediate: true", text);
            Assert.Contains("scheduler: TaskScheduler.Default", text);
            Assert.Contains("suppressEquality: false", text);
            Assert.Contains("this._onChanged_watchEffect?.Dispose();", text);
            Assert.Contains("this._onChanged_watchEffect = null;", text);
        }

        [Fact]
        public void Honors_Watch_Constructor_Single_Value()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _value;

    [Watch(nameof(Value), immediate: true)]
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
            Assert.Contains("Reactive.Watch(() => Value,", text);
            Assert.Contains("immediate: true", text);
        }

        [Fact]
        public void Generates_Watch_With_Deep_Once_And_EqualityComparer()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _value;

    [Watch(Values = new[] { nameof(Value) }, Deep = true, Once = true, EqualityComparer = ""System.Collections.Generic.EqualityComparer<int>.Default"")]
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
            Assert.Contains("deep: true", text);
            Assert.Contains("once: true", text);
            Assert.Contains("equalityComparer: System.Collections.Generic.EqualityComparer<int>.Default", text);
        }

        [Fact]
        public void Honors_Watch_Constructor_Arguments()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _value;

    [Watch(new[] { nameof(Value) }, immediate: true, deep: true, once: true, scheduler: ""TaskScheduler.Default"", suppressEquality: false)]
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
            Assert.Contains("immediate: true", text);
            Assert.Contains("deep: true", text);
            Assert.Contains("once: true", text);
            Assert.Contains("scheduler: TaskScheduler.Default", text);
            Assert.Contains("suppressEquality: false", text);
            Assert.DoesNotContain("equalityComparer:", text);
        }

        [Fact]
        public void Generates_Watch_For_Multiple_Values_And_Maps_Params()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField] private int _a;
    [ReactiveField] private int _b;

    [Watch(Values = new[] { nameof(A), nameof(B) })]
    public void OnAB(int n, int o) { }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            Assert.Contains("Reactive.Watch(() => (A, B),", text);
            Assert.Contains("(newValue, oldValue) => this.OnAB(newValue, oldValue)", text);
        }

        [Fact]
        public void Reports_EFSP2002_When_Watch_Has_No_Values()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Watch]
    public void OnChanged(int n, int o) { }

    [ReactiveField]
    private int _value;
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP2002");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Error, diag!.Severity);
        }

        [Fact]
        public void Reports_EFSP2001_When_Watch_Method_Has_TooMany_Params()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Watch(Values = new[] { nameof(Value) })]
    public void OnChanged(int a, int b, int c) { }

    [ReactiveField]
    private int _value;
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP2001");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Error, diag!.Severity);
        }
    }
}
