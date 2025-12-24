using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace EffectSharp.SourceGenerators.Model
{
    internal readonly struct ModelSpec
    {
        public readonly TypeSpec Type;
        public readonly ImmutableArray<ReactiveFieldSpec> ReactiveFields;
        public readonly ImmutableArray<ComputedSpec> Computeds;
        public readonly ImmutableArray<ComputedListSpec> ComputedLists;
        public readonly ImmutableArray<FunctionCommandSpec> FunctionCommands;
        public readonly ImmutableArray<WatchSpec> Watches;

        public ModelSpec(
            TypeSpec type,
            ImmutableArray<ReactiveFieldSpec> reactiveFields,
            ImmutableArray<ComputedSpec> computeds,
            ImmutableArray<ComputedListSpec> computedLists,
            ImmutableArray<FunctionCommandSpec> functionCommands,
            ImmutableArray<WatchSpec> watches)
        {
            Type = type;
            ReactiveFields = reactiveFields;
            Computeds = computeds;
            ComputedLists = computedLists;
            FunctionCommands = functionCommands;
            Watches = watches;
        }
    }

    internal readonly struct ModelSpecResult
    {
        public static readonly ModelSpecResult Empty = new ModelSpecResult(default, ImmutableArray<Diagnostic>.Empty);

        public readonly ModelSpec Spec;
        public readonly ImmutableArray<Diagnostic> Diagnostics;

        public bool HasSpec => Spec.Type.Header != null;

        public ModelSpecResult(ModelSpec spec, ImmutableArray<Diagnostic> diagnostics)
        {
            Spec = spec;
            Diagnostics = diagnostics;
        }
    }
}
