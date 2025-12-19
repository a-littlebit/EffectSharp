using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class MultiCompilationTests
    {
        [Fact]
        public void Idempotent_When_Sources_Unchanged()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _x;
}
";
            var (comp1, result1, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen1 = result1.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen1);
            var text1 = gen1!.GetText()!.ToString();

            var (comp2, result2, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen2 = result2.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen2);
            var text2 = gen2!.GetText()!.ToString();

            Assert.Equal(text1, text2);
        }

        [Fact]
        public void Regenerates_When_Model_Members_Change_Add_Field()
        {
            var src1 = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
}
";
            var (comp1, result1, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src1);

            var gen1 = result1.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen1);
            var text1 = gen1!.GetText()!.ToString();

            var src2 = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _count;
}
";
            var (comp2, result2, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src2);

            var gen2 = result2.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen2);
            var text2 = gen2!.GetText()!.ToString();

            Assert.NotEqual(text1, text2);
            Assert.Contains("public int Count", text2);
        }

        [Fact]
        public void Regenerates_When_Model_Members_Change_Remove_Field()
        {
            var src1 = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ReactiveField]
    private int _value;
}
";
            var (comp1, result1, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src1);

            var gen1 = result1.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen1);
            var text1 = gen1!.GetText()!.ToString();
            Assert.Contains("public int Value", text1);

            var src2 = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
}
";
            var (comp2, result2, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src2);

            var gen2 = result2.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen2);
            var text2 = gen2!.GetText()!.ToString();

            Assert.NotEqual(text1, text2);
            Assert.DoesNotContain("public int Value", text2);
        }
    }
}
