using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace EffectSharp.SourceGenerators.Context
{
    internal class ComputedListContext
    {
        public IMethodSymbol MethodSymbol { get; }
        public AttributeData AttributeData { get; }
        public INamedTypeSymbol ElementType { get; }
        public INamedTypeSymbol ListType { get; }
        public string PropertyName { get; }

        public string FieldName { get; }

        public string EffectFieldName => FieldName + "_bindEffect";

        public string KeySelector { get; }

        public string EqualityComparer { get; }

        public ComputedListContext(IMethodSymbol method, ReactiveModelContext modelContext)
        {
            MethodSymbol = method;
            var attr = method.GetAttributeData("ComputedList");
            if (attr == null)
                return;

            // Validate return type implements IList<T>
            var ilistType = modelContext.KnownTypes.Get("System.Collections.Generic.IList`1");
            if (ilistType == null || method.ReturnType is not INamedTypeSymbol listType)
            {
                modelContext.ProductionContext.Report(
                    DiagnosticHelper.ComputedListInvalidReturnType,
                    method,
                    [method.Name]);
                return;
            }

            if (!listType.TryGetGenericArgument(ilistType, 0, out var elementType))
            {
                modelContext.ProductionContext.Report(
                    DiagnosticHelper.ComputedListInvalidReturnType,
                    method,
                    [method.Name]);
                return;
            }

            ElementType = elementType;
            ListType = listType;

            // Optional key selector
            KeySelector = attr.GetNamedArgument<string>("KeySelector");

            // Optional equality comparer
            EqualityComparer = attr.GetNamedArgument<string>("EqualityComparer");

            PropertyName = method.Name.StartsWith("Compute")
                ? method.Name.Substring("Compute".Length)
                : "Computed" + method.Name;
            FieldName = "_" + NameHelper.ToCamelCase(PropertyName);

            AttributeData = attr;
        }
    }
}
