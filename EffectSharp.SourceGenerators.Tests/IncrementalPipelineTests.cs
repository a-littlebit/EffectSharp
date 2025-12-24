using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class IncrementalPipelineTests
    {
        [Fact]
        public void Changes_Affect_Only_Touched_Model()
        {
            // Arrange initial sources: two models A and B.
            var modelA_v1 = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class A
{
    [ReactiveField] private int _x;
}
";
            var modelB_v1 = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class B
{
    [ReactiveField] private int _y;
}
";

            var compilationV1 = CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                modelA_v1,
                modelB_v1);

            var generator = new ReactiveModelGenerator();
            var parseOptions = (CSharpParseOptions)compilationV1.SyntaxTrees.First().Options;
            var driver = CSharpGeneratorDriver.Create(generator).WithUpdatedParseOptions(parseOptions);

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilationV1, out _, out var diagnosticsV1);
            Assert.DoesNotContain(diagnosticsV1, d => d.Severity == DiagnosticSeverity.Error);

            var resultV1 = driver.GetRunResult();
            var aTextV1 = GetGeneratedText(resultV1, "A.ReactiveModel.g.cs");
            var bTextV1 = GetGeneratedText(resultV1, "B.ReactiveModel.g.cs");

            // Act: update only model A (add another reactive field), keep model B unchanged.
            var modelA_v2 = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class A
{
    [ReactiveField] private int _x;
    [ReactiveField] private int _z;
}
";

            var compilationV2 = CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                modelA_v2,
                modelB_v1);

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilationV2, out _, out var diagnosticsV2);
            Assert.DoesNotContain(diagnosticsV2, d => d.Severity == DiagnosticSeverity.Error);

            var resultV2 = driver.GetRunResult();
            var aTextV2 = GetGeneratedText(resultV2, "A.ReactiveModel.g.cs");
            var bTextV2 = GetGeneratedText(resultV2, "B.ReactiveModel.g.cs");

            // Assert: A changed, B stayed identical.
            Assert.NotEqual(aTextV1, aTextV2);
            Assert.Contains("public int X", aTextV1);
            Assert.Contains("public int Z", aTextV2);
            Assert.Equal(bTextV1, bTextV2);
        }

        [Fact]
        public void Unrelated_Changes_Do_Not_Regenerate()
        {
            var model = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField] private int _value;
}
";

            var unrelated_v1 = "public class Unrelated { public void M() { } }";
            var unrelated_v2 = "public class Unrelated { public void M() { } // comment change }";

            var compilationV1 = CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                model,
                unrelated_v1);

            var generator = new ReactiveModelGenerator();
            var parseOptions = (CSharpParseOptions)compilationV1.SyntaxTrees.First().Options;
            var driver = CSharpGeneratorDriver.Create(generator).WithUpdatedParseOptions(parseOptions);

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilationV1, out _, out var diagnosticsV1);
            Assert.DoesNotContain(diagnosticsV1, d => d.Severity == DiagnosticSeverity.Error);
            var resultV1 = driver.GetRunResult();
            var textV1 = GetGeneratedText(resultV1, "Sample.ReactiveModel.g.cs");

            var compilationV2 = CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                model,
                unrelated_v2);

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilationV2, out _, out var diagnosticsV2);
            Assert.DoesNotContain(diagnosticsV2, d => d.Severity == DiagnosticSeverity.Error);
            var resultV2 = driver.GetRunResult();
            var textV2 = GetGeneratedText(resultV2, "Sample.ReactiveModel.g.cs");

            Assert.Equal(textV1, textV2);
        }

        private static Compilation CreateCompilation(params string[] sources)
        {
            return GeneratorTestHelper.CreateCompilation(sources);
        }

        private static string GetGeneratedText(GeneratorDriverRunResult result, string hintName)
        {
            var tree = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith(hintName));
            Assert.NotNull(tree);
            return tree!.GetText()!.ToString();
        }
    }
}
