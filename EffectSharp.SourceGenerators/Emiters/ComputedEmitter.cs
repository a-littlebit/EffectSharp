using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Emiters
{
    internal class ComputedEmitter : IReactiveModelEmitter
    {
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
        }

        static void EmitDefinition(ComputedContext computedContext, IndentedTextWriter iw)
        {
            iw.WriteLine($"private Computed<{computedContext.ValueTypeName}> {computedContext.FieldName};");
            if (string.IsNullOrEmpty(computedContext.Setter))
                iw.WriteLine($"public {computedContext.ValueTypeName} {computedContext.PropertyName} => {computedContext.FieldName}.Value;");
            else
                iw.WriteLine($"public {computedContext.ValueTypeName} {computedContext.PropertyName} "
                    + $"{{ get => {computedContext.FieldName}.Value; set => {computedContext.FieldName}.Value = value; }}");
            iw.WriteLine();
        }

        static void EmitInitializer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var computedContext in modelContext.ComputedContexts)
            {
                iw.Write($"this.{computedContext.FieldName} = " +
                    $"Reactive.Computed<{computedContext.ValueTypeName}>(() => this.{computedContext.MethodSymbol.Name}()");
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
    }
}
