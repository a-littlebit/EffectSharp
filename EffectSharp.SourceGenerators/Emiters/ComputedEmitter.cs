using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal class ComputedEmitter : IReactiveModelEmitter
    {
        public void RequireTypes(KnownTypeRegistry registry)
        {
            // No specific types required
        }

        public IncrementalValuesProvider<INamedTypeSymbol> Subcribe(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<INamedTypeSymbol> modelProvider)
        {
            var methods = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: "EffectSharp.SourceGenerators.ComputedAttribute",
                    predicate: static (node, _) => node is MethodDeclarationSyntax,
                    transform: static (ctx, _) => (IMethodSymbol)ctx.TargetSymbol)
                .Where(static m => m is not null)
                .Select(static (m, _) => (m, m.GetAttributeData("EffectSharp.SourceGenerators.ComputedAttribute")))
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

        public void Emit(ReactiveModelContext context, IndentedTextWriter writer)
        {
            context.ComputedContexts = context.ModelSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new ComputedContext(m, context))
                .Where(m => m.AttributeData != null)
                .ToList();

            foreach (var computedContext in context.ComputedContexts)
            {
                EmitDefinition(computedContext, writer);
            }

            context.RegisterInitializer(EmitInitializer);
            context.RegisterDisposer(EmitDisposer);
        }

        static void EmitDefinition(ComputedContext computedContext, IndentedTextWriter iw)
        {
            var valueType = computedContext.ValueTypeName;
            iw.WriteLine($"private Computed<{valueType}> {computedContext.FieldName};");
            if (string.IsNullOrEmpty(computedContext.Setter))
                iw.WriteLine($"public {valueType} {computedContext.PropertyName} => {computedContext.FieldName}.Value;");
            else
                iw.WriteLine($"public {valueType} {computedContext.PropertyName} "
                    + $"{{ get => {computedContext.FieldName}.Value; set => {computedContext.FieldName}.Value = value; }}");
            iw.WriteLine();
        }

        static void EmitInitializer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var computedContext in modelContext.ComputedContexts)
            {
                var valueType = computedContext.ValueTypeName;
                iw.Write($"this.{computedContext.FieldName} = " +
                    $"Reactive.Computed<{valueType}>(() => this.{computedContext.MethodSymbol.Name}()");
                if (!string.IsNullOrEmpty(computedContext.Setter))
                {
                    iw.WriteLine(",");
                    iw.Indent++;
                    iw.Write($"setter: {computedContext.Setter}");
                    iw.Indent--;
                }
                iw.WriteLine(");");

                // Subscribe to PropertyChanged of the computed to raise PropertyChanged for the property
                iw.WriteLine($"this.{computedContext.FieldName}.PropertyChanging += (s, e) =>");
                iw.Indent++;
                iw.WriteLine($"this.PropertyChanging?.Invoke(this, new System.ComponentModel.PropertyChangingEventArgs(nameof(this.{computedContext.PropertyName})));");
                iw.Indent--;

                // Subscribe to PropertyChanged of the computed to raise PropertyChanged for the property
                iw.WriteLine($"this.{computedContext.FieldName}.PropertyChanged += (s, e) =>");
                iw.Indent++;
                iw.WriteLine($"this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(this.{computedContext.PropertyName})));");
                iw.Indent--;

                iw.WriteLine();
            }
        }

        static void EmitDisposer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var computedContext in modelContext.ComputedContexts)
            {
                iw.WriteLine($"this.{computedContext.FieldName}?.Dispose();");
                iw.WriteLine($"this.{computedContext.FieldName} = null;");
                iw.WriteLine();
            }
        }
    }
}
