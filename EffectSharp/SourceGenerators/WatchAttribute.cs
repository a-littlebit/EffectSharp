using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WatchAttribute : Attribute
    {
        public string[] Values { get; set; }

        public bool Immediate { get; set; } = false;

        public bool Deep { get; set; } = false;

        public bool Once { get; set; } = false;

        public string Scheduler { get; set; } = "null";

        public bool SuppressEquality { get; set; } = true;

        public string EqualityComparer { get; set; } = "null";
    }
}
