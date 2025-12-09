using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp
{
    /// <summary>
    /// An attribute to mark a property as reactive with optional default value, reactivity, and depth.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class ReactivePropertyAttribute : Attribute
    {
        public object Default { get; set; }
        public bool Reactive { get; }
        public bool Deep { get; }

        public ReactivePropertyAttribute(object defaultValue = null, bool reactive = true, bool deep = false)
        {
            Default = defaultValue;
            Reactive = reactive;
            Deep = deep;
        }
    }
}
