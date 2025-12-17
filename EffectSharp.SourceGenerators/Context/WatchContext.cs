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
        public List<string> Values { get; }

        public bool IsValid { get;  }
        public string FieldName => $"_{NameHelper.ToCamelCase(MethodSymbol.Name)}_watchEffect";

        public int ParameterCount => MethodSymbol.Parameters.Length;

        public bool Immediate { get; set; }

        public bool Deep { get; set; }

        public bool Once { get; set; }

        public string Scheduler { get; set; }

        public bool SuppressEquality { get; set; }

        public string EqualityComparer { get; set; }

        public WatchContext(IMethodSymbol methodSymbol, ReactiveModelContext modelContext)
        {
            MethodSymbol = methodSymbol;
            AttributeData = methodSymbol.GetAttributeData("Watch");
            if (AttributeData == null)
                return;

            Values = AttributeData.GetNamedArgumentList<string>("Values");
            if (Values == null || Values.Count == 0)
                return;

            if (ParameterCount > 2)
            {
                modelContext.ProductionContext.Report(
                    DiagnosticDescriptors.WatchMethodTooManyParameters,
                    MethodSymbol,
                    MethodSymbol.Name);
                return;
            }

            Immediate = AttributeData.GetNamedArgument("Immediate", false);
            Deep = AttributeData.GetNamedArgument("Deep", false);
            Once = AttributeData.GetNamedArgument("Once", false);
            Scheduler = AttributeData.GetNamedArgument<string>("Scheduler");
            SuppressEquality = AttributeData.GetNamedArgument("SuppressEquality", false);
            EqualityComparer = AttributeData.GetNamedArgument<string>("EqualityComparer");

            IsValid = true;
        }
    }
}
