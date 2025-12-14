using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    public class ReactiveModelContext
    {
        public GeneratorExecutionContext GeneratorContext { get; set; }

        public INamedTypeSymbol ModelSymbol { get; }

        internal List<ReactiveFieldContext> ReactiveFields { get; set; }

        public List<Action<ReactiveModelContext, IndentedTextWriter>> Initializers { get; } = [];

        internal List<FunctionCommandContext> FunctionCommands { get; set; }

        public ReactiveModelContext(GeneratorExecutionContext generatorContext, INamedTypeSymbol reactiveModelSymbol)
        {
            GeneratorContext = generatorContext;
            ModelSymbol = reactiveModelSymbol;
        }

        public void RegisterInitializer(Action<ReactiveModelContext, IndentedTextWriter> initializer)
        {
            Initializers.Add(initializer);
        }

        public void EmitInitializers(IndentedTextWriter iw)
        {
            foreach (var initializer in Initializers)
            {
                initializer(this, iw);
            }
        }
    }
}
