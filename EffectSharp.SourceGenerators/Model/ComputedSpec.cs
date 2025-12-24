namespace EffectSharp.SourceGenerators.Model
{
    internal readonly struct ComputedSpec
    {
        public readonly string FieldName;
        public readonly string PropertyName;
        public readonly string MethodName;
        public readonly string ValueTypeName;
        public readonly string? Setter;

        public bool HasSetter => !string.IsNullOrEmpty(Setter);

        public ComputedSpec(string fieldName, string propertyName, string methodName, string valueTypeName, string? setter)
        {
            FieldName = fieldName;
            PropertyName = propertyName;
            MethodName = methodName;
            ValueTypeName = valueTypeName;
            Setter = setter;
        }
    }
}
