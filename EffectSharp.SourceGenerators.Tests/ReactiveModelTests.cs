using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class ReactiveModelTests
    {
        [Fact]
        public void Generates_Nested_Generic_Type_Signatures_With_Constraints()
        {
            var src = @"
using EffectSharp.SourceGenerators;

namespace My.App
{
    public partial class Outer<T> where T : class
    {
        [ReactiveModel]
        public partial class Inner<U> where U : new()
        {
        }
    }
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("My.App.Outer.Inner.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            // Outer wrapper with generic parameter and constraint
            Assert.Contains("public partial class Outer<T>", text);
            Assert.Contains("where T : class", text);

            // Inner model with generic parameter and constraint
            Assert.Contains("public partial class Inner<U>", text);
            Assert.Contains("where U : new()", text);

            // Base list includes missing interfaces on the model type
            Assert.Contains(": System.ComponentModel.INotifyPropertyChanging, System.ComponentModel.INotifyPropertyChanged, IReactive", text);
        }

        [Fact]
        public void Skips_Duplicated_Interfaces_And_Existing_Events()
        {
            var src = @"
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class Sample : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            // Header should include only missing interfaces (no duplicate INotifyPropertyChanged)
            Assert.Contains(": System.ComponentModel.INotifyPropertyChanging, IReactive", text);
            Assert.DoesNotContain("System.ComponentModel.INotifyPropertyChanged, IReactive", text);

            // Event generation should skip PropertyChanged (already defined) but emit PropertyChanging
            Assert.Contains("PropertyChangingEventHandler PropertyChanging;", text);
            Assert.DoesNotContain("PropertyChangedEventHandler PropertyChanged;", text);
        }

        [Fact]
        public void Skips_Event_Generation_When_Base_Type_Defines_Events()
        {
            var src = @"
using EffectSharp.SourceGenerators;

public class Base : System.ComponentModel.INotifyPropertyChanged, System.ComponentModel.INotifyPropertyChanging
{
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    public event System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
}

[ReactiveModel]
public partial class Sample : Base
{
}
";
            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                src);

            var gen = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("Sample.ReactiveModel.g.cs"));
            Assert.NotNull(gen);
            var text = gen.GetText()!.ToString();

            // Header should not redundantly add interfaces (already provided by base via AllInterfaces)
            Assert.DoesNotContain("System.ComponentModel.INotifyPropertyChanging, System.ComponentModel.INotifyPropertyChanged", text);

            // Events should not be generated since base type already defines them
            Assert.DoesNotContain("PropertyChangedEventHandler PropertyChanged;", text);
            Assert.DoesNotContain("PropertyChangingEventHandler PropertyChanging;", text);
        }

        [Fact]
        public void Uses_Unique_Filenames_Per_Namespace()
        {
            var srcA = @"
using EffectSharp.SourceGenerators;
namespace A { [ReactiveModel] public partial class Sample { } }
";
            var srcB = @"
using EffectSharp.SourceGenerators;
namespace B { [ReactiveModel] public partial class Sample { } }
";

            var (comp, result, driver) = GeneratorTestHelper.RunGenerator(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                srcA, srcB);

            var genA = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("A.Sample.ReactiveModel.g.cs"));
            var genB = result.GeneratedTrees.SingleOrDefault(t => t.FilePath.EndsWith("B.Sample.ReactiveModel.g.cs"));
            Assert.NotNull(genA);
            Assert.NotNull(genB);
        }
    }
}
