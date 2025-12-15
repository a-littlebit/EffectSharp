using EffectSharp.SourceGenerators;
using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Emmiters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

[Generator]
public sealed class ReactiveModelGenerator : ISourceGenerator
{
    private static readonly IReactiveModelEmitter[] _emitters =
    {
        new ReactiveFieldEmitter(),
        new FunctionCommandEmitter(),
        new ComputedEmitter(),
        new WatchEmitter(),
        new InitializerEmitter()
    };

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        Debugger.Launch();
#endif
    }

    public void Execute(GeneratorExecutionContext context)
    {
        foreach (var tree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (symbol == null)
                    continue;

                if (!symbol.HasAttribute("ReactiveModelAttribute"))
                    continue;

                EmitModel(context, symbol);
            }
        }
    }

    private static void EmitModel(
        GeneratorExecutionContext context,
        INamedTypeSymbol model)
    {
        var sw = new StringWriter();
        var iw = new IndentedTextWriter(sw, "    ");

        iw.WriteLine("using EffectSharp;");
        iw.WriteLine();

        var ns = model.ContainingNamespace;
        if (!ns.IsGlobalNamespace)
        {
            iw.WriteLine("namespace " + ns.ToDisplayString());
            iw.WriteLine("{");
            iw.Indent++;
        }

        iw.WriteLine(
            "public partial class " + model.Name +
            " : System.ComponentModel.INotifyPropertyChanging, " +
            "System.ComponentModel.INotifyPropertyChanged, IReactive");
        iw.WriteLine("{");
        iw.Indent++;

        iw.WriteLine(
            "public event System.ComponentModel.PropertyChangingEventHandler PropertyChanging;");
        iw.WriteLine(
            "public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");
        iw.WriteLine();

        var contextModel = new ReactiveModelContext(context, model);
        foreach (var emitter in _emitters)
        {
            emitter.Emit(contextModel, iw);
            iw.WriteLine();
        }

        iw.Indent--;
        iw.WriteLine("}");

        if (!ns.IsGlobalNamespace)
        {
            iw.Indent--;
            iw.WriteLine("}");
        }

        context.AddSource(
            model.Name + ".Reactive.g.cs",
            SourceText.From(sw.ToString(), Encoding.UTF8));
    }
}
