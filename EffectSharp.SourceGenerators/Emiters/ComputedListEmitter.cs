using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal class ComputedListEmitter : IReactiveModelEmitter
    {
        public void RequireTypes(KnownTypeRegistry registry)
        {
            registry.Require("System.Collections.Generic.IList`1");
        }

        public IncrementalValuesProvider<INamedTypeSymbol> Subcribe(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<INamedTypeSymbol> modelProvider)
        {
            var methods = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "EffectSharp.SourceGenerators.ComputedListAttribute",
                    predicate: static (node, _) => node is MethodDeclarationSyntax,
                    transform: static (ctx, _) => (IMethodSymbol)ctx.TargetSymbol)
                .Where(static m => m is not null)
                .Select(static (m, _) => (m, m.GetAttributeData("EffectSharp.SourceGenerators.ComputedListAttribute")))
                .WithComparer(EqualityComparer<(IMethodSymbol, AttributeData)>.Default)
                .Select(static (pair, _) => pair.Item1)
                .Collect();

            return modelProvider.Combine(methods)
                .Select(static (pair, _) =>
                {
                    var (modelSymbol, methods) = pair;
                    return (modelSymbol, methods.Where(m => SymbolEqualityComparer.Default.Equals(m.ContainingType, modelSymbol)).ToImmutableArray());
                })
                .WithComparer(EqualityComparer<(INamedTypeSymbol, ImmutableArray<IMethodSymbol>)>.Default)
                .Select((pair, _) => pair.Item1);
        }

        public void Emit(ReactiveModelContext context, IndentedTextWriter iw)
        {
            context.ComputedListContexts = context.ModelSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new ComputedListContext(m, context))
                .Where(c => c.AttributeData != null)
                .ToList();

            foreach (var lc in context.ComputedListContexts)
            {
                EmitDefinition(lc, iw);
            }

            context.RegisterInitializer(EmitInitializer);
            context.RegisterDisposer(EmitDisposer);
        }

        private static void EmitDefinition(ComputedListContext lc, IndentedTextWriter iw)
        {
            var elem = lc.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            iw.WriteLine($"private ReactiveCollection<{elem}> {lc.FieldName} = new ReactiveCollection<{elem}>();");
            iw.WriteLine($"private Effect {lc.EffectFieldName};");
            iw.WriteLine($"public ReactiveCollection<{elem}> {lc.PropertyName} => {lc.FieldName};");
            iw.WriteLine();
        }

        private static void EmitInitializer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var lc in modelContext.ComputedListContexts)
            {
                if (!string.IsNullOrWhiteSpace(lc.KeySelector))
                {
                    var equalityComparer = string.IsNullOrWhiteSpace(lc.EqualityComparer) ? "null" : lc.EqualityComparer;
                    iw.WriteLine($"this.{lc.EffectFieldName} = this.{lc.FieldName}.BindTo(() => this.{lc.MethodSymbol.Name}(), {lc.KeySelector}, {equalityComparer});");
                }
                else
                {
                    var elem = lc.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var equalityComparer = string.IsNullOrWhiteSpace(lc.EqualityComparer)
                        ? $"(System.Collections.Generic.IEqualityComparer<{elem}>)null"
                        : lc.EqualityComparer;
                    iw.WriteLine($"this.{lc.EffectFieldName} = this.{lc.FieldName}.BindTo(() => this.{lc.MethodSymbol.Name}(), {equalityComparer});");
                }
                iw.WriteLine();
            }
        }

        private static void EmitDisposer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var lc in modelContext.ComputedListContexts)
            {
                iw.WriteLine($"this.{lc.EffectFieldName}?.Dispose();");
                iw.WriteLine($"this.{lc.EffectFieldName} = null;");
                iw.WriteLine();
            }
        }
    }
}
