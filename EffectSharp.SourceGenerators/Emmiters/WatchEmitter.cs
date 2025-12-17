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
    internal class WatchEmitter : IReactiveModelEmitter
    {
        public void Emit(ReactiveModelContext context, IndentedTextWriter writer)
        {
            context.WatchContexts = context.ModelSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new WatchContext(m, context))
                .Where(m => m.IsValid)
                .ToList();

            foreach (var watchContext in context.WatchContexts)
            {
                EmitDefinition(watchContext, writer);
            }

            context.RegisterInitializer(EmitInitializer);
        }

        static void EmitDefinition(WatchContext watchContext, IndentedTextWriter writer)
        {
            writer.WriteLine($"private Effect {watchContext.FieldName};");
        }

        static void EmitInitializer(ReactiveModelContext modelContext, IndentedTextWriter writer)
        {
            foreach (var watchContext in modelContext.WatchContexts)
            {
                var valuesExpr = string.Join(", ", watchContext.Values);
                if (watchContext.Values.Count > 1)
                    valuesExpr = "(" + valuesExpr + ")";
                writer.WriteLine($"this.{watchContext.FieldName} = Reactive.Watch(() => {valuesExpr},");
                writer.Indent++;
                writer.Write($"(newValue, oldValue) => this.{watchContext.MethodSymbol.Name}(");
                if (watchContext.ParameterCount > 0)
                    writer.Write("newValue");
                if (watchContext.ParameterCount > 1)
                    writer.Write(", oldValue");
                writer.Write(")");

                void WriteOption(string name, string value)
                {
                    writer.WriteLine(",");
                    writer.Write($"{name}: {value}");
                }

                if (watchContext.Immediate) WriteOption("Immediate", "true");
                if (watchContext.Deep) WriteOption("deep", "true");
                if (watchContext.Once) WriteOption("once", "true");
                if (!string.IsNullOrWhiteSpace(watchContext.Scheduler)) WriteOption("scheduler", watchContext.Scheduler);
                if (watchContext.SupressEquality)
                    WriteOption("supressEquality", "true");
                else if (!string.IsNullOrWhiteSpace(watchContext.EqualityComparer))
                    WriteOption("equalityComparer", watchContext.EqualityComparer);

                writer.WriteLine(");");
                writer.Indent--;
                writer.WriteLine();
            }
        }
    }
}
