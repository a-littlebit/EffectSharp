using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp
{
    /// <summary>
    /// Indicates that the marked class or member should not participate in reactive effect tracking
    /// or notifications.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public sealed class NonReactive : Attribute
    {
    }
}
