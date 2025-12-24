using System;

namespace EffectSharp.SourceGenerators
{
    /// <summary>
    /// Marks a method returning <see cref="System.Collections.Generic.IList{T}"/> as a computed list;
    /// a reactive collection is generated and kept in sync.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ComputedListAttribute : Attribute
    {
        /// <summary>
        /// Optional key selector used to diff items (e.g. "item =&gt; item.Id").
        /// </summary>
        public string? KeySelector { get; set; }

        /// <summary>
        /// Optional equality comparer expression for items (e.g. "ItemComparer.Instance").
        /// </summary>
        public string? EqualityComparer { get; set; }
    }
}
