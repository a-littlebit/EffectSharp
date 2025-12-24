using System;
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
        private static ImmutableArray<ComputedListSpec> BuildComputedLists(INamedTypeSymbol modelSymbol, Compilation compilation, ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var iListT = compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
            var builder = ImmutableArray.CreateBuilder<ComputedListSpec>();
            foreach (var method in modelSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                var attr = SymbolHelper.GetAttribute(method, "ComputedList");
                if (attr == null)
                    continue;

                if (iListT == null || !(method.ReturnType is INamedTypeSymbol listType) || !SymbolHelper.TryGetGenericTypeArgument(listType, iListT, 0, out var elementType))
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.ComputedListInvalidReturnType, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                var propertyName = method.Name.StartsWith("Compute", StringComparison.Ordinal)
                    ? method.Name.Substring("Compute".Length)
                    : "Computed" + method.Name;
                var fieldName = "_" + NameHelper.ToCamel(propertyName);
                var effectName = fieldName + "_bindEffect";
                var keySelector = SymbolHelper.GetNamedArgument<string?>(attr, "KeySelector", null, out _);
                var equalityComparer = SymbolHelper.GetNamedArgument<string?>(attr, "EqualityComparer", null, out _);

                var elementTypeName = elementType!.ToDisplayString(FullName);

                builder.Add(new ComputedListSpec(
                    fieldName,
                    effectName,
                    propertyName,
                    method.Name,
                    elementTypeName,
                    keySelector,
                    equalityComparer));
            }
            return builder.ToImmutable();
        }

        private static void EmitComputedLists(ModelSpec spec, IndentedTextWriter iw)
        {
            if (spec.ComputedLists.Length == 0)
                return;

            foreach (var lc in spec.ComputedLists)
            {
                iw.WriteLine("private ReactiveCollection<" + lc.ElementTypeName + "> " + lc.FieldName + " = new ReactiveCollection<" + lc.ElementTypeName + ">();");
                iw.WriteLine("private Effect " + lc.EffectFieldName + ";");
                iw.WriteLine("public ReactiveCollection<" + lc.ElementTypeName + "> " + lc.PropertyName + " => " + lc.FieldName + ";");
                iw.WriteLine();
            }
        }

        private static void EmitComputedListInitializers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var lc in spec.ComputedLists)
            {
                if (lc.HasKeySelector)
                {
                    var comparer = lc.HasEqualityComparer ? lc.EqualityComparer : "null";
                    iw.WriteLine("this." + lc.EffectFieldName + " = this." + lc.FieldName + ".BindTo(() => this." + lc.MethodName + "(), " + lc.KeySelector + ", " + comparer + ");");
                }
                else
                {
                    var comparer = lc.HasEqualityComparer ? lc.EqualityComparer : "(System.Collections.Generic.IEqualityComparer<" + lc.ElementTypeName + ">)null";
                    iw.WriteLine("this." + lc.EffectFieldName + " = this." + lc.FieldName + ".BindTo(() => this." + lc.MethodName + "(), " + comparer + ");");
                }
                iw.WriteLine();
            }
        }

        private static void EmitComputedListDisposers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var lc in spec.ComputedLists)
            {
                iw.WriteLine("this." + lc.EffectFieldName + "?.Dispose();");
                iw.WriteLine("this." + lc.EffectFieldName + " = null;");
            }
            if (spec.ComputedLists.Length > 0)
                iw.WriteLine();
        }
    }
}
