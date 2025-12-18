using EffectSharp.SourceGenerators;
using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
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

[Generator]
public sealed class ReactiveModelGenerator : IIncrementalGenerator
{
    private static readonly IReactiveModelEmitter[] _emitters =
    {
        new ReactiveFieldEmitter(),
        new FunctionCommandEmitter(),
        new ComputedEmitter(),
        new ComputedListEmitter(),
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

        // Emit containing type wrappers if nested
        var containingTypes = new Stack<INamedTypeSymbol>();
        var ct = model.ContainingType;
        while (ct != null)
        {
            containingTypes.Push(ct);
            ct = ct.ContainingType;
        }

        foreach (var t in containingTypes)
        {
            iw.WriteLine(ComposeTypeHeader(t, baseList: null));
            // Constraints for outer types
            WriteTypeParameterConstraints(iw, t);
            iw.WriteLine("{");
            iw.Indent++;
        }

        // Compose base list for the model only when interfaces are not already implemented
        var baseInterfaces = GetMissingInterfaces(compilation, model);
        string baseList = baseInterfaces.Length > 0 ? (": " + string.Join(", ", baseInterfaces)) : null;

        iw.WriteLine(ComposeTypeHeader(model, baseList));
        WriteTypeParameterConstraints(iw, model);
        iw.WriteLine("{");
        iw.Indent++;

        // Only emit events if not already declared on the type
        bool hasPropertyChanging = model.GetMembers("PropertyChanging").OfType<IEventSymbol>().Any();
        bool hasPropertyChanged = model.GetMembers("PropertyChanged").OfType<IEventSymbol>().Any();
        if (!hasPropertyChanging)
        {
            iw.WriteLine(
                "public event System.ComponentModel.PropertyChangingEventHandler PropertyChanging;");
        }
        if (!hasPropertyChanged)
        {
            iw.WriteLine(
                "public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;");
        }
        iw.WriteLine();

        var contextModel = new ReactiveModelContext(compilation, context, model);
        foreach (var emitter in _emitters)
        {
            emitter.Emit(contextModel, iw);
            iw.WriteLine();
        }

        iw.Indent--;
        iw.WriteLine("}");

        // Close containing types if any
        foreach (var _ in containingTypes)
        {
            iw.Indent--;
            iw.WriteLine("}");
        }

        if (!ns.IsGlobalNamespace)
        {
            iw.Indent--;
            iw.WriteLine("}");
        }

        // Use a unique hint name to avoid collisions across namespaces/nesting
        var hintName = NameHelper.GetReactiveHintFileName(model);
        context.AddSource(
            hintName,
            SourceText.From(sw.ToString(), Encoding.UTF8));
    }

    private static string ComposeTypeHeader(INamedTypeSymbol type, string baseList)
    {
        var accessibility = type.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "public"
        };

        var modifiers = new List<string> { accessibility, "partial" };
        if (type.IsStatic) modifiers.Add("static");
        if (type.IsSealed && !type.IsRecord) modifiers.Add("sealed");
        if (type.IsAbstract && !type.IsRecord) modifiers.Add("abstract");

        var kind = type.IsRecord ? "record" : (type.TypeKind == TypeKind.Struct ? "struct" : "class");
        var typeParams = type.TypeParameters.Length > 0
            ? "<" + string.Join(", ", type.TypeParameters.Select(tp => tp.Name)) + ">"
            : string.Empty;

        var header = string.Join(" ", modifiers) + " " + kind + " " + type.Name + typeParams;
        if (!string.IsNullOrEmpty(baseList))
            header += " " + baseList;
        return header;
    }

    private static void WriteTypeParameterConstraints(IndentedTextWriter iw, INamedTypeSymbol type)
    {
        foreach (var tp in type.TypeParameters)
        {
            var constraints = new List<string>();
            if (tp.HasReferenceTypeConstraint) constraints.Add("class");
            if (tp.HasValueTypeConstraint) constraints.Add("struct");
            if (tp.HasNotNullConstraint) constraints.Add("notnull");
            if (tp.HasUnmanagedTypeConstraint) constraints.Add("unmanaged");
            constraints.AddRange(tp.ConstraintTypes.Select(t => t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            if (tp.HasConstructorConstraint) constraints.Add("new()");
            if (constraints.Count > 0)
            {
                iw.WriteLine($"where {tp.Name} : {string.Join(", ", constraints)}");
            }
        }
    }

    private static string[] GetMissingInterfaces(Compilation compilation, INamedTypeSymbol model)
    {
        var ifaceChanged = compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged");
        var ifaceChanging = compilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanging");
        var iReactive = compilation.GetTypeByMetadataName("EffectSharp.IReactive");

        bool hasChanged = ifaceChanged != null && model.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, ifaceChanged));
        bool hasChanging = ifaceChanging != null && model.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, ifaceChanging));
        bool hasReactive = iReactive != null && model.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iReactive));

        var list = new List<string>();
        if (!hasChanging) list.Add("System.ComponentModel.INotifyPropertyChanging");
        if (!hasChanged) list.Add("System.ComponentModel.INotifyPropertyChanged");
        if (!hasReactive) list.Add("IReactive");
        return list.ToArray();
    }
}
