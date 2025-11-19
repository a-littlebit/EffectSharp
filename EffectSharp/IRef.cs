using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp;

public interface IRef<T> : IReactive
{
    T Value { get; set; }
}
