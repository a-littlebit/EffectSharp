using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators
{
    /// <summary>
    /// Marks a method to receive change notifications for specified reactive values via <c>Reactive.Watch</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class WatchAttribute : Attribute
    {
        /// <summary>
        /// Expressions to watch (e.g. "Foo", "(Bar, Baz)"), passed directly into <c>Reactive.Watch</c>.
        /// </summary>
        public string[]? Values { get; set; }

        /// <summary>
        /// Invoke the handler immediately with the current value when true.
        /// </summary>
        public bool Immediate { get; set; } = false;

        /// <summary>
        /// Enable deep watching of nested reactive objects when true.
        /// </summary>
        public bool Deep { get; set; } = false;

        /// <summary>
        /// Stop watching after the first notification when true.
        /// </summary>
        public bool Once { get; set; } = false;

        /// <summary>
        /// Optional scheduler expression used to run the watch (e.g. "TaskScheduler.Default").
        /// </summary>
        public string? Scheduler { get; set; } = "null";

        /// <summary>
        /// When true, suppress equality checks between old and new values (default). Set false to enable equality checks.
        /// </summary>
        public bool SuppressEquality { get; set; } = true;

        /// <summary>
        /// Optional equality comparer expression when equality checks are enabled.
        /// </summary>
        public string? EqualityComparer { get; set; } = "null";

        public WatchAttribute(string[]? values = null, bool immediate = false, bool deep = false, bool once = false, string? scheduler = "null", bool suppressEquality = true, string? equalityComparer = "null")
        {
            Values = values;
            Immediate = immediate;
            Deep = deep;
            Once = once;
            Scheduler = scheduler;
            SuppressEquality = suppressEquality;
            EqualityComparer = equalityComparer;
        }

        public WatchAttribute(string value, bool immediate = false, bool deep = false, bool once = false, string? scheduler = "null", bool suppressEquality = true, string? equalityComparer = "null")
            : this(new[] { value }, immediate, deep, once, scheduler, suppressEquality, equalityComparer)
        {
        }
    }
}
