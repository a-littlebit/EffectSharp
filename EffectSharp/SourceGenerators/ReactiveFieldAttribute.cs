using System;

namespace EffectSharp.SourceGenerators
{
    /// <summary>
    /// Marks a backing field to generate a reactive property wrapper.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ReactiveFieldAttribute : Attribute
    {
        /// <summary>
        /// Custom equality comparison method name. Empty or null disables equality checks.
        /// The method must be callable as <c>bool AreEqual(T oldValue, T newValue)</c> and the type must match the field type.
        /// </summary>
        public string EqualsMethod { get; set; } = DefaultEqualsMethod;

        /// <summary>
        /// Default equality comparison used when no custom method is provided; <c>T</c> is replaced with the field type.
        /// </summary>
        public const string DefaultEqualsMethod = "global::System.Collections.Generic.EqualityComparer<T>.Default";

        public ReactiveFieldAttribute(string equalsMethod = DefaultEqualsMethod)
        {
            EqualsMethod = equalsMethod;
        }
    }
}
