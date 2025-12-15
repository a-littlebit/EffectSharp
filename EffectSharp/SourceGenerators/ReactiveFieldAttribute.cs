using System;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ReactiveFieldAttribute : Attribute
    {
        /// <summary>
        /// Custom equality comparison method.
        /// 
        /// If not null or empty (skip equality comparison), it must refer to a callable method that:
        /// - Returns bool
        /// - Accepts (oldValue, newValue)
        /// - Parameters are compatible with the field type
        /// 
        /// Examples:
        /// - "AreEqual"
        /// - "MyComparer.AreEqual"
        /// - "global::MyNamespace.MyComparer.AreEqual"
        /// </summary>
        public string EqualsMethod { get; set; } = DefaultEqualsMethod;

        /// <summary>
        /// Default equality comparison method used when no custom method is specified.
        /// "&lt;T&gt;" will be replaced with the actual field type.
        /// </summary>
        public const string DefaultEqualsMethod = "global::System.Collections.Generic.EqualityComparer<T>.Default";
    }
}
