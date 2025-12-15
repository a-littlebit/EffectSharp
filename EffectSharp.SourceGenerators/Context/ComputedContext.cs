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

        public ComputedContext(IMethodSymbol method)
        {
            MethodSymbol = method;
            AttributeData = method.GetAttributeData("Computed");
            if (AttributeData == null)
                return;
            SetterMethod = AttributeData.GetNamedArgument<string>("SetterMethod");
        }
    }
}
