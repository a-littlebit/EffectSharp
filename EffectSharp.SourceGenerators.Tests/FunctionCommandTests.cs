using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class FunctionCommandTests
    {
        [Fact]
        public void Generates_Basic_FunctionCommand()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [FunctionCommand]
    public int Do(string param) { }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);
            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("private IFunctionCommand<string, int> _doCommand;", text);
            Assert.Contains("public IFunctionCommand<string, int> DoCommand => _doCommand;", text);
            Assert.Contains("this._doCommand = FunctionCommand.Create<string, int>(", text);
            Assert.Contains("this._doCommand?.Dispose();", text);
            Assert.Contains("this._doCommand = null;", text);
        }

        [Fact]
        public void Generates_Async_FunctionCommand()
        {
            var src = @"
using EffectSharp.SourceGenerators;
using System.Threading;
using System.Threading.Tasks;

[ReactiveModel]
public partial class Sample
{
    [FunctionCommand]
    public async Task<int> DoAsync(string param, CancellationToken ct) { await Task.Delay(1, ct); return 42; }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);
            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("private IAsyncFunctionCommand<string, int> _doAsyncCommand;", text);
            Assert.Contains("public IAsyncFunctionCommand<string, int> DoAsyncCommand => _doAsyncCommand;", text);
            Assert.Contains("this._doAsyncCommand = FunctionCommand.CreateFromTask<string, int>(", text);
        }

        [Fact]
        public void Generates_FunctionCommand_With_CanExecute_And_Disable_Concurrent()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [FunctionCommand(CanExecute = nameof(CanRun), AllowConcurrentExecution = false)]
    public void Do(int p) { }
    public bool CanRun() => true;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains(", canExecute: CanRun", text);
            Assert.Contains(", allowConcurrentExecution: false", text);
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

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains(") => this.Do(param, cancellationToken)", text);
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

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();
            Assert.Contains("executionScheduler: TaskScheduler.Default", text);
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

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP1001");
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

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP1002");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Warning, diag!.Severity);
        }
    }
}
