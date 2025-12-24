using System.Collections.Immutable;

namespace EffectSharp.SourceGenerators.Model
{
    internal readonly struct WatchSpec
    {
        public readonly string FieldName;
        public readonly string MethodName;
        public readonly ImmutableArray<string> Values;
        public readonly int ParameterCount;
        public readonly bool Immediate;
        public readonly bool Deep;
        public readonly bool Once;
        public readonly string? Scheduler;
        public readonly bool SuppressEquality;
        public readonly string? EqualityComparer;

        public bool HasScheduler => !string.IsNullOrWhiteSpace(Scheduler);
        public bool HasEqualityComparer => !string.IsNullOrWhiteSpace(EqualityComparer);

        public WatchSpec(
            string fieldName,
            string methodName,
            ImmutableArray<string> values,
            int parameterCount,
            bool immediate,
            bool deep,
            bool once,
            string? scheduler,
            bool suppressEquality,
            string? equalityComparer)
        {
            FieldName = fieldName;
            MethodName = methodName;
            Values = values;
            ParameterCount = parameterCount;
            Immediate = immediate;
            Deep = deep;
            Once = once;
            Scheduler = scheduler;
            SuppressEquality = suppressEquality;
            EqualityComparer = equalityComparer;
        }
    }
}
