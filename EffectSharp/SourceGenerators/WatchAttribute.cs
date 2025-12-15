using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WatchAttribute : Attribute
    {
        public string[] Properties { get; set; }

        public string Options { get; set; }
    }
}
