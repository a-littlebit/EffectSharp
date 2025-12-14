using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    public class ReactiveModelContext
    {
        public GeneratorExecutionContext GeneratorContext { get; set; }

        public INamedTypeSymbol ModelSymbol { get; }

        public List<ReactiveFieldContext> ReactiveFields { get; set; }

        public ReactiveModelContext(GeneratorExecutionContext generatorContext, INamedTypeSymbol reactiveModelSymbol)
        {
            GeneratorContext = generatorContext;
            ModelSymbol = reactiveModelSymbol;
        }
    }
}
