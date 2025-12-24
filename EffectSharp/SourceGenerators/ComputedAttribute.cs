using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    /// <summary>
    /// Marks a parameterless method as a computed value; a property and computed cache are generated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ComputedAttribute : Attribute
    {
        /// <summary>
        /// Optional setter expression to allow assigning to the computed value (e.g. "SetFoo").
        /// </summary>
        public string? Setter { get; set; }

        public ComputedAttribute(string? setter = null)
        {
            Setter = setter;
        }
    }
}
