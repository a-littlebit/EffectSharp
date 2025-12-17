using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ComputedAttribute : Attribute
    {
        public string Setter { get; set; }
    }
}
