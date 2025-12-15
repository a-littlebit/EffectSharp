using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class ReactiveModelGeneratorTests
    {
        private static IEnumerable<Diagnostic> AllDiags(GeneratorDriverRunResult result)
            => result.Diagnostics.Concat(result.Results.SelectMany(r => r.Diagnostics));

        [Fact]
        public void Generates_Reactive_Model_For_Fields_Computed_Command_Watch()
        {
            var src = @"
using System.Threading;
using System.Threading.Tasks;
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _count;

    [Computed]
    public int Current() => Count + 1;

    [Computed]
    public string ComputeDisplay() => $""#{ComputedCurrent}""; // property: Display

    [FunctionCommand(CanExecute = nameof(CanInc), AllowConcurrentExecution = false)]
    public async Task Inc()
    {
        await Task.Delay(10);
    }

    public bool CanInc() => Count < 10;

    [Watch(Properties = new[] { nameof(Count) })]
    public void OnCountChanged(int n, int o) { }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            Assert.Contains("public partial class Sample : System.ComponentModel.INotifyPropertyChanging, System.ComponentModel.INotifyPropertyChanged, IReactive", text);
            Assert.Contains("public int Count", text);
            Assert.Contains("_count_dependency.Track();", text);
            Assert.Contains("TaskManager.QueueNotification", text);
            Assert.Contains("public int ComputedCurrent =>", text);
            Assert.Contains("public string Display =>", text);
            Assert.Contains("public IAsyncFunctionCommand<object> IncCommand =>", text);
            Assert.Contains("public void InitializeReactiveModel()", text);
            Assert.Contains("Reactive.Watch(() => Count,", text);
        }

        [Fact]
        public void Generates_EqualsMethod_Default_For_ReactiveField()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField(EqualsMethod = ""global::System.Collections.Generic.EqualityComparer<T>.Default"")]
    private int _x;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("EqualityComparer<int>.Default.Equals(_x, value)", text);
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

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("if (Cmp.AreEqual(_x, value))", text);
        }

        [Fact]
        public void Generates_Computed_SetterMethod_When_Provided()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Computed(SetterMethod = ""SetTotal"")]
    public int Total() => 42;
    public void SetTotal(int v) { }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("setter: (value) => SetTotal(value)", text);
        }

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

    [Watch(Properties = new[] { nameof(Value) }, Options = ""new WatchOptions<int> { Immediate = true }"")]
    public void OnChanged(int n, int o) { }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("new WatchOptions<int> { Immediate = true }", text);
        }

        [Fact]
        public void Generates_FunctionCommand_With_ExecutionScheduler_For_Async()
        {
            var src = @"
using System.Threading;
using System.Threading.Tasks;
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [FunctionCommand(ExecutionScheduler = ""TaskScheduler.Default"")]
    public async Task Do(CancellationToken ct) { await Task.Delay(1); }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("executionScheduler: TaskScheduler.Default", text);
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

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("return _x.Value;", text);
            Assert.Contains("_x.Value = value;", text);
        }

        [Fact]
        public void Generates_FunctionCommand_Parameter_Mapping_For_Param_And_CT()
        {
            var src = @"
using System.Threading;
using System.Threading.Tasks;
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [FunctionCommand]
    public async Task Do(int p, CancellationToken ct) { await Task.Delay(1); }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains(") => this.Do(param, cancellationToken)", text);
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

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.Reactive.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            // ComputeTotal -> Total
            Assert.Contains("public int Total =>", text);
            // Total -> ComputedTotal
            Assert.Contains("public int ComputedTotal =>", text);
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

            var diag = AllDiags(result).FirstOrDefault(d => d.Id == "EFSP3001");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Error, diag!.Severity);
        }

        [Fact]
        public void Reports_EFSP1001_When_FunctionCommand_Has_TooMany_Params()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [FunctionCommand]
    public void Do(int a, int b) { }
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = AllDiags(result).FirstOrDefault(d => d.Id == "EFSP1001");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Error, diag!.Severity);
        }

        [Fact]
        public void Reports_EFSP1002_When_Scheduler_On_NonAsync_FunctionCommand()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [FunctionCommand(ExecutionScheduler = ""TaskScheduler.Default"")]
    public void Do() { }
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = AllDiags(result).FirstOrDefault(d => d.Id == "EFSP1002");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Warning, diag!.Severity);
        }

        [Fact]
        public void Reports_EFSP2001_When_Watch_Method_Has_TooMany_Params()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [Watch(Properties = new[] { nameof(Value) })]
    public void OnChanged(int a, int b, int c) { }

    [ReactiveField]
    private int _value;
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = AllDiags(result).FirstOrDefault(d => d.Id == "EFSP2001");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Error, diag!.Severity);
        }
    }
}
