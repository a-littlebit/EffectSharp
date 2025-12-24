namespace EffectSharp.SourceGenerators.Model
{
    internal readonly struct ComputedListSpec
    {
        public readonly string FieldName;
        public readonly string EffectFieldName;
        public readonly string PropertyName;
        public readonly string MethodName;
        public readonly string ElementTypeName;
        public readonly string? KeySelector;
        public readonly string? EqualityComparer;

        public bool HasKeySelector => !string.IsNullOrWhiteSpace(KeySelector);
        public bool HasEqualityComparer => !string.IsNullOrWhiteSpace(EqualityComparer);

        public ComputedListSpec(
            string fieldName,
            string effectFieldName,
            string propertyName,
            string methodName,
            string elementTypeName,
            string? keySelector,
            string? equalityComparer)
        {
            FieldName = fieldName;
            EffectFieldName = effectFieldName;
            PropertyName = propertyName;
            MethodName = methodName;
            ElementTypeName = elementTypeName;
            KeySelector = keySelector;
            EqualityComparer = equalityComparer;
        }
    }
}
