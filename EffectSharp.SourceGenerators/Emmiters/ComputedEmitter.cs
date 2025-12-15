using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Emmiters
{
    internal class ComputedEmitter : IReactiveModelEmitter
    {
        public void Emit(ReactiveModelContext context, IndentedTextWriter writer)
        {
            context.ComputedContexts = context.ModelSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new ComputedContext(m))
                .Where(m => m.AttributeData != null)
                .ToList();

            foreach (var computedContext in context.ComputedContexts)
            {
                EmitDefinition(computedContext, writer);
            }

            context.RegisterInitializer(EmitInitializer);
        }

        static void EmitDefinition(ComputedContext computedContext, IndentedTextWriter iw)
        {
            iw.WriteLine($"private Computed<{computedContext.ValueTypeName}> {computedContext.FieldName};");
            iw.WriteLine($"public {computedContext.ValueTypeName} {computedContext.PropertyName} => {computedContext.FieldName}.Value;");
            iw.WriteLine();
        }

        static void EmitInitializer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var computedContext in modelContext.ComputedContexts)
            {
                iw.Write($"this.{computedContext.FieldName} = " +
                    $"Reactive.Computed<{computedContext.ValueTypeName}>(() => {computedContext.MethodSymbol.Name}()");
                if (!string.IsNullOrEmpty(computedContext.SetterMethod))
                {
                    iw.WriteLine(",");
                    iw.Indent++;
                    iw.Write($"setter: (value) => {computedContext.SetterMethod}(value)");
                    iw.Indent--;
                }
                iw.WriteLine(");");
                iw.WriteLine($"this.{computedContext.FieldName}.PropertyChanged += (s, e) =>");
                iw.Indent++;
                iw.WriteLine($"this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(this.{computedContext.PropertyName})));");
                iw.Indent--;
                iw.WriteLine();
            }
        }
    }
}
