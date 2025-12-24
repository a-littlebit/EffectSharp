using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    /// <summary>
    /// Marks a method to generate an <c>IFunctionCommand</c> or <c>IAsyncFunctionCommand</c> wrapper.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class FunctionCommandAttribute : Attribute
    {
        /// <summary>
        /// Optional method name used to determine whether the command can execute (e.g. "CanRun").
        /// </summary>
        public string? CanExecute { get; set; }

        /// <summary>
        /// Allows running multiple executions simultaneously when true; otherwise enforces sequential execution.
        /// </summary>
        public bool AllowConcurrentExecution { get; set; } = true;

        /// <summary>
        /// Scheduler expression for async commands (e.g. "TaskScheduler.Default"). Ignored for sync methods.
        /// </summary>
        public string? ExecutionScheduler { get; set; } = "";

        public FunctionCommandAttribute(string? canExecute = null, bool allowConcurrentExecution = true, string? executionScheduler = "")
        {
            CanExecute = canExecute;
            AllowConcurrentExecution = allowConcurrentExecution;
            ExecutionScheduler = executionScheduler;
        }
    }
}
