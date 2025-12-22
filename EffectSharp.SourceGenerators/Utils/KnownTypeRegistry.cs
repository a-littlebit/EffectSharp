using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace EffectSharp.SourceGenerators.Utils
{
    // Collects metadata-name requirements and builds a KnownTypes provider once.
    public sealed class KnownTypeRegistry
    {
        private readonly HashSet<string> _requirements = new();

        public void Require(string metadataName)
        {
            _requirements.Add(metadataName);
        }

        public void RequireRange(IEnumerable<string> metadataNames)
        {
            foreach (var name in metadataNames)
            {
                _requirements.Add(name);
            }
        }

        public IncrementalValueProvider<KnownTypes> Build(IncrementalValueProvider<Compilation> compilation)
        {
            return compilation
                .Select((c, _) =>
                {
                    var builder = ImmutableDictionary.CreateBuilder<string, INamedTypeSymbol>(StringComparer.Ordinal);
                    foreach (var metadataName in _requirements)
                    {
                        var type = c.GetTypeByMetadataName(metadataName);
                        if (type != null)
                            builder[metadataName] = type;
                    }
                    return new KnownTypes(builder.ToImmutable());
                });
        }
    }
}
