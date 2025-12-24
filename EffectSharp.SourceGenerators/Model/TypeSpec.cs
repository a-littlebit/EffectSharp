using System.Collections.Immutable;

namespace EffectSharp.SourceGenerators.Model
{
    internal readonly struct TypeSpec
    {
        public readonly string Namespace;
        public readonly ImmutableArray<ContainingTypeSpec> ContainingTypes;
        public readonly string Header;
        public readonly ImmutableArray<string> Constraints;
        public readonly bool EmitPropertyChangingEvent;
        public readonly bool EmitPropertyChangedEvent;
        public readonly string HintName;

        public TypeSpec(
            string @namespace,
            ImmutableArray<ContainingTypeSpec> containingTypes,
            string header,
            ImmutableArray<string> constraints,
            bool emitPropertyChangingEvent,
            bool emitPropertyChangedEvent,
            string hintName)
        {
            Namespace = @namespace;
            ContainingTypes = containingTypes;
            Header = header;
            Constraints = constraints;
            EmitPropertyChangingEvent = emitPropertyChangingEvent;
            EmitPropertyChangedEvent = emitPropertyChangedEvent;
            HintName = hintName;
        }
    }

    internal readonly struct ContainingTypeSpec
    {
        public readonly string Header;
        public readonly ImmutableArray<string> Constraints;

        public ContainingTypeSpec(string header, ImmutableArray<string> constraints)
        {
            Header = header;
            Constraints = constraints;
        }
    }
}
