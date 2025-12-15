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
