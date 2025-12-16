using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;
using System.Linq;

namespace EffectSharp.SourceGenerators.Emmiters
{
    internal class ComputedListEmitter : IReactiveModelEmitter
    {
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
        }

        private static void EmitDefinition(ComputedListContext lc, IndentedTextWriter iw)
        {
            iw.WriteLine($"private ReactiveCollection<{lc.ElementType.ToDisplayString()}> {lc.FieldName} = new ReactiveCollection<{lc.ElementType.ToDisplayString()}>();");
            iw.WriteLine($"public ReactiveCollection<{lc.ElementType.ToDisplayString()}> {lc.PropertyName} => {lc.FieldName};");
            iw.WriteLine();
        }

        private static void EmitInitializer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var lc in modelContext.ComputedListContexts)
            {
                if (!string.IsNullOrWhiteSpace(lc.KeySelector))
                {
                    var equalityComparer = string.IsNullOrWhiteSpace(lc.EqualityComparer) ? "null" : lc.EqualityComparer;
                    iw.WriteLine($"this.{lc.FieldName}.BindTo(() => this.{lc.MethodSymbol.Name}(), {lc.KeySelector}, {equalityComparer});");
                }
                else
                {
                    var equalityComparer = string.IsNullOrWhiteSpace(lc.EqualityComparer)
                        ? $"(System.Collections.Generic.IEqualityComparer<{lc.ElementType.ToDisplayString()}>)null"
                        : lc.EqualityComparer;
                    iw.WriteLine($"this.{lc.FieldName}.BindTo(() => this.{lc.MethodSymbol.Name}(), {equalityComparer});");
                }
                iw.WriteLine();
            }
        }
    }
}
