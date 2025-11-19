using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp;

public interface IReactive
{
    Dependency? GetDependency(string propertyName);
}
