using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp;

/// <summary>
/// An interface for reactive references that hold a value of type T.
/// </summary>
/// <typeparam name="T">The type of the value held by the reference. </typeparam>
public interface IRef<T> : IReactive
{
    T Value { get; set; }
}
