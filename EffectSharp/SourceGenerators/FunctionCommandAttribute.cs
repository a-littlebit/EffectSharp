using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class FunctionCommandAttribute : Attribute
    {
        public string CanExecute { get; set; }

        public bool AllowConcurrentExecution { get; set; } = true;

        public string ExecutionScheduler { get; set; } = "";
    }
}
