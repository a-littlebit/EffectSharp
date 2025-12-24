using System;
using System.Collections.Immutable;
using System.CodeDom.Compiler;
using System.Linq;
using Microsoft.CodeAnalysis;
using EffectSharp.SourceGenerators.Model;
using EffectSharp.SourceGenerators.Utils;

namespace EffectSharp.SourceGenerators
{
    public sealed partial class ReactiveModelGenerator
    {
        private static ImmutableArray<DeepTrackSpec> BuildDeepTracks(INamedTypeSymbol modelSymbol, Compilation compilation, ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var iReactive = compilation.GetTypeByMetadataName("EffectSharp.IReactive");
            var iListT = compilation.GetTypeByMetadataName("System.Collections.Generic.IList`1");
            var reactiveCollectionT = compilation.GetTypeByMetadataName("EffectSharp.ReactiveCollection`1");

            var builder = ImmutableArray.CreateBuilder<DeepTrackSpec>();
            int tempIndex = 0;

            foreach (var member in modelSymbol.GetMembers())
            {
                var deepAttr = SymbolHelper.GetAttribute(member, "Deep");
                if (deepAttr == null)
                    continue;

                switch (member)
                {
                    case IFieldSymbol field:
                        if (SymbolHelper.GetAttribute(field, "ReactiveField") != null)
                            continue;

                        AddIfReactive(field.Type, "this." + field.Name, NameHelper.ToCamel("deep_" + NameHelper.RemoveLeadingUnderscore(field.Name)), field.Locations.FirstOrDefault());
                        break;

                    case IPropertySymbol property:
                        if (property.Parameters.Length > 0)
                            continue;

                        AddIfReactive(property.Type, "this." + property.Name, NameHelper.ToCamel("deep_" + property.Name), property.Locations.FirstOrDefault());
                        break;

                    case IMethodSymbol method:
                        var computedAttr = SymbolHelper.GetAttribute(method, "Computed");
                        var computedListAttr = SymbolHelper.GetAttribute(method, "ComputedList");
                        if (computedAttr == null && computedListAttr == null)
                        {
                            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.DeepMethodMustBeComputed, method.Locations.FirstOrDefault(), method.Name));
                            continue;
                        }

                        var propertyName = method.Name.StartsWith("Compute", StringComparison.Ordinal)
                            ? method.Name.Substring("Compute".Length)
                            : "Computed" + method.Name;
                        var expr = "this." + propertyName;

                        if (computedAttr != null)
                        {
                            AddIfReactive(method.ReturnType, expr, NameHelper.ToCamel("deep_" + propertyName), method.Locations.FirstOrDefault());
                        }
                        else if (computedListAttr != null)
                        {
                            ITypeSymbol? reactiveCollection = null;
                            if (iListT != null && reactiveCollectionT != null && method.ReturnType is INamedTypeSymbol listType && SymbolHelper.TryGetGenericTypeArgument(listType, iListT, 0, out var elementType) && elementType != null)
                            {
                                reactiveCollection = reactiveCollectionT.Construct(elementType);
                            }

                            AddIfReactive(reactiveCollection ?? method.ReturnType, expr, NameHelper.ToCamel("deep_" + propertyName), method.Locations.FirstOrDefault());
                        }
                        break;
                }
            }

            return builder.ToImmutable();

            void AddIfReactive(ITypeSymbol? type, string expression, string baseName, Location? location)
            {
                var must = MustBeReactive(type, iReactive);
                var may = MayBeReactive(type, iReactive);
                if (!must && !may)
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.DeepTargetMustBeReactive, location, expression.Replace("this.", string.Empty)));
                    return;
                }

                var temp = string.IsNullOrWhiteSpace(baseName) ? "deep" + tempIndex : baseName + tempIndex;
                tempIndex++;
                builder.Add(new DeepTrackSpec(expression, temp, may, must));
            }
        }

        private static void EmitTrackDeep(ModelSpec spec, IndentedTextWriter iw)
        {
            iw.WriteLine("public void TrackDeep()");
            iw.WriteLine("{");
            iw.Indent++;

            foreach (var field in spec.ReactiveFields)
            {
                iw.WriteLine(field.DependencyFieldName + ".Track();");
                if (field.MayBeReactive)
                {
                    iw.WriteLine("if (" + field.ReadExpression + " is IReactive r_" + field.BackingFieldName + ")");
                    iw.WriteLine("{");
                    iw.Indent++;
                    iw.WriteLine("r_" + field.BackingFieldName + ".TrackDeep();");
                    iw.Indent--;
                    iw.WriteLine("}");
                }
                else if (field.MustBeReactive)
                {
                    iw.WriteLine(field.ReadExpression + "?.TrackDeep();");
                }

                iw.WriteLine();
            }

            foreach (var deep in spec.DeepTracks)
            {
                if (deep.MustBeReactive)
                {
                    iw.WriteLine(deep.Expression + "?.TrackDeep();");
                }
                else if (deep.MayBeReactive)
                {
                    iw.WriteLine("var " + deep.TempName + " = " + deep.Expression + ";");
                    iw.WriteLine("if (" + deep.TempName + " is IReactive r_" + deep.TempName + ")");
                    iw.WriteLine("{");
                    iw.Indent++;
                    iw.WriteLine("r_" + deep.TempName + ".TrackDeep();");
                    iw.Indent--;
                    iw.WriteLine("}");
                }

                iw.WriteLine();
            }

            iw.Indent--;
            iw.WriteLine("}");
            iw.WriteLine();
        }
    }
}