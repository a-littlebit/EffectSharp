using System;

namespace EffectSharp.SourceGenerators
{
    /// <summary>
    /// Marks a partial class as a reactive model so the source generator emits the backing implementation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ReactiveModelAttribute : Attribute
    {
    }
}
