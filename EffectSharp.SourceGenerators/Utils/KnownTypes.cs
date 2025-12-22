using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace EffectSharp.SourceGenerators.Utils
{
    public sealed class KnownTypes
    {
        private readonly ImmutableDictionary<string, INamedTypeSymbol> _map;

        internal KnownTypes(ImmutableDictionary<string, INamedTypeSymbol> map)
        {
            _map = map;
        }

        public INamedTypeSymbol? Get(string key)
        {
            _map.TryGetValue(key, out var value);
            return value;
        }

        public bool TryGet(string key, out INamedTypeSymbol? symbol)
            => _map.TryGetValue(key, out symbol);
    }
}
