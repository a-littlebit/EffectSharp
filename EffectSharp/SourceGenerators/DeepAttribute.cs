using System;

namespace EffectSharp.SourceGenerators
{
    /// <summary>
    /// Marks a member whose value should participate in deep tracking; the value must be or may be <c>IReactive</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class DeepAttribute : Attribute
    {
    }
}