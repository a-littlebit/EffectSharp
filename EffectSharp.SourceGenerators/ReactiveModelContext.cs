using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace EffectSharp.SourceGenerators
{
    internal sealed class ReactiveModelContext
    {
        public INamedTypeSymbol ModelSymbol { get; }
        public IReadOnlyList<IFieldSymbol> ReactiveFields { get; }

        public ReactiveModelContext(
            INamedTypeSymbol modelSymbol,
            IReadOnlyList<IFieldSymbol> reactiveFields)
        {
            ModelSymbol = modelSymbol;
            ReactiveFields = reactiveFields;
        }
    }
}
