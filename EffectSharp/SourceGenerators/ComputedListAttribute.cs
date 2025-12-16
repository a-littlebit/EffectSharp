using System;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ComputedListAttribute : Attribute
    {
        public string KeySelector { get; set; }
        public string EqualityComparer { get; set; }
    }
}
