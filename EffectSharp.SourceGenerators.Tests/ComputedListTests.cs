using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class ComputedListTests
    {
        [Fact]
        public void Generates_ComputedList_Without_Key_Uses_Default_Comparer()
        {
            var src = @"
using System.Collections.Generic;
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ComputedList]
    public List<int> Items() => new List<int>();
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            Assert.Contains("private ReactiveCollection<int> _computedItems = new ReactiveCollection<int>();", text);
            Assert.Contains("public ReactiveCollection<int> ComputedItems => _computedItems;", text);
            Assert.Contains("this._computedItems.BindTo(() => this.Items(), (System.Collections.Generic.IEqualityComparer<int>)null);", text);
        }

        [Fact]
        public void Generates_ComputedList_With_Key_And_Comparer()
        {
            var src = @"
using System.Collections.Generic;
using EffectSharp.SourceGenerators;

static class MyKeyComparer { public static System.Collections.Generic.IEqualityComparer<int> Instance => null; }

[ReactiveModel]
public partial class Sample
{
    [ComputedList(KeySelector = ""item => item"", EqualityComparer = ""MyKeyComparer.Instance"")]
    public List<int> Items() => new List<int>();
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            Assert.Contains("private ReactiveCollection<int> _computedItems = new ReactiveCollection<int>();", text);
            Assert.Contains("public ReactiveCollection<int> ComputedItems => _computedItems;", text);
            Assert.Contains("this._computedItems.BindTo(() => this.Items(), item => item, MyKeyComparer.Instance);", text);
        }

        [Fact]
        public void Accepts_ComputedList_ReturnType_From_Interface_Inheriting_IList()
        {
            var src = @"
using System.Collections.Generic;
using EffectSharp.SourceGenerators;

public interface IMyList<T> : IList<T> { }

[ReactiveModel]
public partial class Sample
{
    [ComputedList]
    public IMyList<int> Items() => null;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            Assert.Contains("private ReactiveCollection<int> _computedItems = new ReactiveCollection<int>();", text);
            Assert.Contains("this._computedItems.BindTo(() => this.Items(), (System.Collections.Generic.IEqualityComparer<int>)null);", text);
        }

        [Fact]
        public void Reports_EFSP4001_When_ComputedList_ReturnType_Not_IList()
        {
            var src = @"
using System.Collections.Generic;
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample
{
    [ComputedList]
    public IEnumerable<int> Items() => null;
}
";
            var (comp, result, _) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var diag = GeneratorTestHelper.AllDiags(result).FirstOrDefault(d => d.Id == "EFSP4001");
            Assert.NotNull(diag);
            Assert.Equal(DiagnosticSeverity.Error, diag!.Severity);
        }
    }
}
