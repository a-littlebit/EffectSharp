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
        /// <summary>
        /// Optional default value used when the proxy is initialized via <see cref="ReactiveProxy{T}.InitializeForValues"/>.
        /// If not specified and the property type is a value type, the runtime default of that type is used.
        /// This setting is ignored when the proxy delegates to a target via <see cref="ReactiveProxy{T}.InitializeForTarget(T)"/>.
        /// </summary>
        public object Default { get; set; }

        /// <summary>
        /// Indicates whether the property participates in dependency tracking and change notifications.
        /// Defaults to <c>true</c>. When <c>false</c>, reads do not track and writes do not trigger notifications.
        /// Applies to both value-storing and target-delegating initialization modes.
        /// </summary>
        public bool Reactive { get; }

        /// <summary>
        /// Enables deep initialization for interface-typed properties when storing values internally.
        /// When <c>true</c> and the property type is an interface, a nested reactive proxy instance is created
        /// during <see cref="ReactiveProxy{T}.InitializeForValues"/>. This does not automatically proxy values
        /// when delegating to an existing target via <see cref="ReactiveProxy{T}.InitializeForTarget(T)"/>.
        /// </summary>
        public bool Deep { get; }

        /// <summary>
        /// Initializes a new instance of the attribute.
        /// </summary>
        /// <param name="defaultValue">Optional default value used only by value-storing proxies; for value types, the runtime default is used when omitted.</param>
        /// <param name="reactive">Whether the property participates in dependency tracking and notifications; defaults to <c>true</c>.</param>
        /// <param name="deep">Whether to create a nested reactive proxy for interface-typed properties during value initialization; defaults to <c>false</c>.</param>
        public ReactivePropertyAttribute(object defaultValue = null, bool reactive = true, bool deep = false)
        {
            Default = defaultValue;
            Reactive = reactive;
            Deep = deep;
        }
    }
}
