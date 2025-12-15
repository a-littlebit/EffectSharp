using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    internal class ComputedContext
    {
        public IMethodSymbol MethodSymbol { get; }
        public AttributeData AttributeData { get; }
        public string SetterMethod { get; }

        public string ValueTypeName => MethodSymbol.ReturnType.ToDisplayString();

        public string PropertyName => MethodSymbol.Name.StartsWith("Compute")
            ? MethodSymbol.Name.Substring("Compute".Length)
            : "Computed" + MethodSymbol.Name;

        public string FieldName => "_" + NameHelper.ToCamelCase(PropertyName);

        public ComputedContext(IMethodSymbol method, ReactiveModelContext modelContext)
        {
            MethodSymbol = method;
            var attr = method.GetAttributeData("Computed");
            if (attr == null)
                return;

            if (method.Parameters.Length > 0)
            {
                modelContext.ProductionContext.Report(
                    DiagnosticDescriptors.ComputedMethodTooManyParameters,
                    method,
                    method.Name);
                return;
            }

            SetterMethod = attr.GetNamedArgument<string>("SetterMethod");

            AttributeData = attr;
        }
    }
}
