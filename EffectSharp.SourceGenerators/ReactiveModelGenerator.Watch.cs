using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using EffectSharp.SourceGenerators.Model;
using EffectSharp.SourceGenerators.Utils;

namespace EffectSharp.SourceGenerators
{
    public sealed partial class ReactiveModelGenerator
    {
        private static ImmutableArray<WatchSpec> BuildWatches(INamedTypeSymbol modelSymbol, ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var builder = ImmutableArray.CreateBuilder<WatchSpec>();
            foreach (var method in modelSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                var attr = SymbolHelper.GetAttribute(method, "Watch");
                if (attr == null)
                    continue;

                var values = SymbolHelper.GetArrayArgument<string>(attr, "Values");
                if (values.Length == 0)
                    continue;

                if (method.Parameters.Length > 2)
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.WatchMethodTooManyParameters, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                var fieldName = "_" + NameHelper.ToCamel(method.Name) + "_watchEffect";
                var immediate = SymbolHelper.GetNamedArgument(attr, "Immediate", false, out _);
                var deep = SymbolHelper.GetNamedArgument(attr, "Deep", false, out _);
                var once = SymbolHelper.GetNamedArgument(attr, "Once", false, out _);
                var scheduler = SymbolHelper.GetNamedArgument<string?>(attr, "Scheduler", null, out _);
                var suppressEquality = SymbolHelper.GetNamedArgument(attr, "SuppressEquality", true, out _);
                var equalityComparer = SymbolHelper.GetNamedArgument<string?>(attr, "EqualityComparer", null, out _);

                builder.Add(new WatchSpec(
                    fieldName,
                    method.Name,
                    values,
                    method.Parameters.Length,
                    immediate,
                    deep,
                    once,
                    scheduler,
                    suppressEquality,
                    equalityComparer));
            }
            return builder.ToImmutable();
        }

        private static void EmitWatches(ModelSpec spec, IndentedTextWriter iw)
        {
            if (spec.Watches.Length == 0)
                return;

            foreach (var watch in spec.Watches)
            {
                iw.WriteLine("private Effect " + watch.FieldName + ";");
            }
            iw.WriteLine();
        }

        private static void EmitWatchInitializers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var watch in spec.Watches)
            {
                var values = string.Join(", ", watch.Values);
                if (watch.Values.Length > 1)
                {
                    values = "(" + values + ")";
                }
                iw.WriteLine("this." + watch.FieldName + " = Reactive.Watch(() => " + values + ",");
                iw.Indent++;
                iw.Write("(newValue, oldValue) => this." + watch.MethodName + "(");
                if (watch.ParameterCount > 0)
                    iw.Write("newValue");
                if (watch.ParameterCount > 1)
                    iw.Write(", oldValue");
                iw.Write(")");

                if (watch.Immediate)
                {
                    iw.WriteLine(",");
                    iw.Write("immediate: true");
                }
                if (watch.Deep)
                {
                    iw.WriteLine(",");
                    iw.Write("deep: true");
                }
                if (watch.Once)
                {
                    iw.WriteLine(",");
                    iw.Write("once: true");
                }
                if (watch.HasScheduler)
                {
                    iw.WriteLine(",");
                    iw.Write("scheduler: " + watch.Scheduler);
                }
                if (!watch.SuppressEquality)
                {
                    iw.WriteLine(",");
                    iw.Write("suppressEquality: false");
                }
                else if (watch.HasEqualityComparer)
                {
                    iw.WriteLine(",");
                    iw.Write("equalityComparer: " + watch.EqualityComparer);
                }

                iw.WriteLine(");");
                iw.Indent--;
                iw.WriteLine();
            }
        }

        private static void EmitWatchDisposers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var watch in spec.Watches)
            {
                iw.WriteLine("this." + watch.FieldName + "?.Dispose();");
                iw.WriteLine("this." + watch.FieldName + " = null;");
            }
        }
    }
}
