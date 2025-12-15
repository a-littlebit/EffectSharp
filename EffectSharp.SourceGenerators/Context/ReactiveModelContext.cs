using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    public class ReactiveModelContext
    {
        public Compilation Compilation { get; set; }

        public SourceProductionContext ProductionContext { get; set; }

        public INamedTypeSymbol ModelSymbol { get; }

        internal List<ReactiveFieldContext> ReactiveFields { get; set; }

        public List<Action<ReactiveModelContext, IndentedTextWriter>> Initializers { get; } = [];

        internal List<FunctionCommandContext> FunctionCommands { get; set; }

        internal List<ComputedContext> ComputedContexts { get; set; }

        internal List<WatchContext> WatchContexts { get; set; }

        public ReactiveModelContext(
            Compilation compilation,
            SourceProductionContext productionContext,
            INamedTypeSymbol reactiveModelSymbol)
        {
            Compilation = compilation;
            ProductionContext = productionContext;
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
