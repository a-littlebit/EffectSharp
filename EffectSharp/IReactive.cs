using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// An interface for reactive objects that can provide dependencies for their properties.
    /// </summary>
    public interface IReactive
    {
        void TrackDeep();
    }
}
