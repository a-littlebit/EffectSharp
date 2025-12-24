namespace EffectSharp.SourceGenerators.Model
{
    internal readonly struct DeepTrackSpec
    {
        public readonly string Expression;
        public readonly string TempName;
        public readonly bool MayBeReactive;
        public readonly bool MustBeReactive;

        public DeepTrackSpec(string expression, string tempName, bool mayBeReactive, bool mustBeReactive)
        {
            Expression = expression;
            TempName = tempName;
            MayBeReactive = mayBeReactive;
            MustBeReactive = mustBeReactive;
        }
    }
}