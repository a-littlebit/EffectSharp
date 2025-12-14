using System;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ReactiveFieldAttribute : Attribute
    {
        /// <summary>
        /// Custom equality comparison method.
        /// 
        /// If not null or empty, it must be <see cref="NoEqualityComparison"/> or refer to a callable method that:
        /// - Returns bool
        /// - Accepts (oldValue, newValue)
        /// - Parameters are compatible with the field type
        /// 
        /// Examples:
        /// - "AreEqual"
        /// - "MyComparer.AreEqual"
        /// - "global::MyNamespace.MyComparer.AreEqual"
        /// </summary>
        public string EqualsMethod { get; set; }

        /// <summary>
        /// Used in <see cref="EqualsMethod"/> to indicate that no equality comparison should be performed.
        /// </summary>
        public const string NoEqualityComparison = "noEqualityComparison";
    }
}
