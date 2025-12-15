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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

[Generator]
public sealed class ReactiveModelGenerator : IIncrementalGenerator
{
    private static readonly IReactiveModelEmitter[] _emitters =
    {
        new ReactiveFieldEmitter(),
        new FunctionCommandEmitter(),
        new ComputedEmitter(),
        new WatchEmitter(),
        new InitializerEmitter()
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if DEBUG
        if (!Debugger.IsAttached)
        {
            // Debugger.Launch();
        }
#endif

        // Create an IncrementalValueProvider to filter classes with ReactiveModelAttribute
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine compilation and collected class symbols
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Register source output
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        // Only consider class declarations with attributes
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };
    }

    private static INamedTypeSymbol GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (symbol == null)
            return null;

        // Ensure the class has ReactiveModelAttribute
        if (!symbol.HasAttribute("ReactiveModelAttribute"))
            return null;

        return symbol;
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> classes,
        SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
            return;

        // Generate code for each class (deduplicated, cast to INamedTypeSymbol)
        foreach (var classSymbol in classes
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>())
        {
            EmitModel(compilation, classSymbol, context);
        }
    }

    private static void EmitModel(
        Compilation compilation,
        INamedTypeSymbol model,
        SourceProductionContext context)
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

        var contextModel = new ReactiveModelContext(compilation, context, model);
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
