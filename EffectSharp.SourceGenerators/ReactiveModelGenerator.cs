using EffectSharp.SourceGenerators;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

[Generator]
public sealed class ReactiveModelGenerator : ISourceGenerator
{
    private static readonly IReactiveModelEmitter[] _emitters =
    {
        new ReactiveFieldEmitter()
    };

    public void Initialize(GeneratorInitializationContext context) { }

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

                var reactiveFields = symbol.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(f => f.HasAttribute("ReactiveFieldAttribute"))
                    .ToList();

                if (reactiveFields.Count == 0)
                    continue;

                EmitModel(context, symbol, reactiveFields);
            }
        }
    }

    private static void EmitModel(
        GeneratorExecutionContext context,
        INamedTypeSymbol model,
        List<IFieldSymbol> fields)
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

        var ctx = new ReactiveModelContext(model, fields);
        foreach (var emitter in _emitters)
        {
            emitter.Emit(ctx, iw);
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
