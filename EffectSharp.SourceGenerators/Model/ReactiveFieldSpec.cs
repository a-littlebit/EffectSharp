namespace EffectSharp.SourceGenerators.Model
{
    internal enum EqualityMode
    {
        None,
        Default,
        Custom
    }

    internal readonly struct ReactiveFieldSpec
    {
        public readonly string BackingFieldName;
        public readonly string DependencyFieldName;
        public readonly string PropertyName;
        public readonly string FieldTypeName;
        public readonly string ReadExpression;
        public readonly string WriteExpression;
        public readonly EqualityMode EqualityMode;
        public readonly string EqualityExpression;
        public readonly bool MayBeReactive;
        public readonly bool MustBeReactive;

        public ReactiveFieldSpec(
            string backingFieldName,
            string dependencyFieldName,
            string propertyName,
            string fieldTypeName,
            string readExpression,
            string writeExpression,
            EqualityMode equalityMode,
            string equalityExpression,
            bool mayBeReactive,
            bool mustBeReactive)
        {
            BackingFieldName = backingFieldName;
            DependencyFieldName = dependencyFieldName;
            PropertyName = propertyName;
            FieldTypeName = fieldTypeName;
            ReadExpression = readExpression;
            WriteExpression = writeExpression;
            EqualityMode = equalityMode;
            EqualityExpression = equalityExpression;
            MayBeReactive = mayBeReactive;
            MustBeReactive = mustBeReactive;
        }
    }
}
