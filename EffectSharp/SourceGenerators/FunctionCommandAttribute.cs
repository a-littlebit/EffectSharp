using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public class FunctionCommandAttribute : Attribute
    {
        public string CanExecute { get; set; }
    }
}
