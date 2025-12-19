using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    public class ReactiveModelContext
    {
        public SourceProductionContext ProductionContext { get; }

        public KnownTypes KnownTypes { get; }

        public INamedTypeSymbol ModelSymbol { get; }

        internal List<ReactiveFieldContext> ReactiveFields { get; set; }

        public List<Action<ReactiveModelContext, IndentedTextWriter>> Initializers { get; } = new();
        public List<Action<ReactiveModelContext, IndentedTextWriter>> Disposers { get; } = new();

        internal List<FunctionCommandContext> FunctionCommands { get; set; }

        internal List<ComputedContext> ComputedContexts { get; set; }

        internal List<ComputedListContext> ComputedListContexts { get; set; }

        internal List<WatchContext> WatchContexts { get; set; }

        public ReactiveModelContext(
            SourceProductionContext productionContext,
            KnownTypes knownTypes,
            INamedTypeSymbol reactiveModelSymbol)
        {
            ProductionContext = productionContext;
            KnownTypes = knownTypes;
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
                if (ProductionContext.CancellationToken.IsCancellationRequested)
                    return;
                initializer(this, iw);
            }
        }

        public void RegisterDisposer(Action<ReactiveModelContext, IndentedTextWriter> disposer)
        {
            Disposers.Add(disposer);
        }

        public void EmitDisposers(IndentedTextWriter iw)
        {
            foreach (var disposer in Disposers)
            {
                if (ProductionContext.CancellationToken.IsCancellationRequested)
                    return;
                disposer(this, iw);
            }
        }
    }
}
