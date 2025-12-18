using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EffectSharp.SourceGenerators.Tests
{
    public class IncrementalPipelineTests
    {
        [Fact]
        public void Incremental_PerModel_Update_OnlyChangedModel()
        {
            var a_v1 = @"
using EffectSharp.SourceGenerators;
namespace A { [ReactiveModel] public partial class Sample { [Computed] public int Total() => 1; } }
";
            var b_v1 = @"
using EffectSharp.SourceGenerators;
namespace B { [ReactiveModel] public partial class Sample { } }
";

            var comp1 = GeneratorTestHelper.CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                a_v1, b_v1);

            var generator = new ReactiveModelGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(comp1);
            var r1 = driver.GetRunResult();

            var srcA1 = r1.Results.SelectMany(r => r.GeneratedSources)
                .First(s => s.HintName.EndsWith("A.Sample.ReactiveModel.g.cs")).SourceText.ToString();
            var srcB1 = r1.Results.SelectMany(r => r.GeneratedSources)
                .First(s => s.HintName.EndsWith("B.Sample.ReactiveModel.g.cs")).SourceText.ToString();

            // Change only A.Sample (add a reactive field), B remains the same
            var a_v2 = @"
using EffectSharp.SourceGenerators;
namespace A { [ReactiveModel] public partial class Sample { [Computed] public int Total() => 1; [ReactiveField] private int _x; } }
";
            var comp2 = GeneratorTestHelper.CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                a_v2, b_v1);

            driver = driver.RunGenerators(comp2);
            var r2 = driver.GetRunResult();

            var srcA2 = r2.Results.SelectMany(r => r.GeneratedSources)
                .First(s => s.HintName.EndsWith("A.Sample.ReactiveModel.g.cs")).SourceText.ToString();
            var srcB2 = r2.Results.SelectMany(r => r.GeneratedSources)
                .First(s => s.HintName.EndsWith("B.Sample.ReactiveModel.g.cs")).SourceText.ToString();

            // Unchanged model B should produce identical output text
            Assert.Equal(srcB1, srcB2);
            // Changed model A should reflect the new reactive field as a property
            Assert.NotEqual(srcA1, srcA2);
            Assert.Contains("public int X", srcA2);
        }

        [Fact]
        public void Incremental_WellKnownTypes_And_BaseList_Update_On_Compilation_Changes()
        {
            // Initially: model does not implement any interfaces
            var model_v1 = @"
using EffectSharp.SourceGenerators;
public partial class Sample
{
    [ReactiveModel]
    public partial class Inner { }
}
";
            var comp1 = GeneratorTestHelper.CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                model_v1);
            var generator = new ReactiveModelGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(comp1);
            var r1 = driver.GetRunResult();
            var text1 = r1.Results.SelectMany(r => r.GeneratedSources)
                .First(s => s.HintName.EndsWith("Sample.Inner.ReactiveModel.g.cs")).SourceText.ToString();
            Assert.Contains(": IReactive, System.ComponentModel.INotifyPropertyChanging, System.ComponentModel.INotifyPropertyChanged", text1);

            // Modify: implement INotifyPropertyChanged on Inner; base list should drop it but keep others
            var model_v2 = @"
using EffectSharp.SourceGenerators;
using System.ComponentModel;
public partial class Sample
{
    [ReactiveModel]
    public partial class Inner : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
";
            var comp2 = GeneratorTestHelper.CreateCompilation(
                GeneratorTestHelper.EffectSharpAttributeStubs,
                GeneratorTestHelper.MinimalEffectSharpRuntimeStubs,
                model_v2);
            driver = driver.RunGenerators(comp2);
            var r2 = driver.GetRunResult();
            var text2 = r2.Results.SelectMany(r => r.GeneratedSources)
                .First(s => s.HintName.EndsWith("Sample.Inner.ReactiveModel.g.cs")).SourceText.ToString();
            Assert.DoesNotContain("System.ComponentModel.INotifyPropertyChanged,", text2);
            Assert.Contains(": IReactive, System.ComponentModel.INotifyPropertyChanging", text2);
        }
    }
}
