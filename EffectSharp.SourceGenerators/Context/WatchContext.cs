using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    internal class WatchContext
    {
        public IMethodSymbol MethodSymbol { get; }
        public AttributeData AttributeData { get; }
        public List<string> Properties { get; }

        public bool IsValid { get;  }
        public string FieldName => $"_{NameHelper.ToCamelCase(MethodSymbol.Name)}_watchEffect";

        public int ParameterCount => MethodSymbol.Parameters.Length;

        public string Options { get; }

        public WatchContext(IMethodSymbol methodSymbol, ReactiveModelContext modelContext)
        {
            MethodSymbol = methodSymbol;
            AttributeData = methodSymbol.GetAttributeData("Watch");
            if (AttributeData == null)
                return;

            Properties = AttributeData.GetNamedArgumentList<string>("Properties");
            if (Properties == null || Properties.Count == 0)
                return;

            if (ParameterCount > 2)
            {
                modelContext.GeneratorContext.Report(
                    DiagnosticDescriptors.WatchMethodTooManyParameters,
                    MethodSymbol,
                    MethodSymbol.Name);
                return;
            }

            var options = AttributeData.GetNamedArgument("Options", "null");
            if (string.IsNullOrWhiteSpace(options))
                options = "null";
            Options = options;

            IsValid = true;
        }
    }
}
