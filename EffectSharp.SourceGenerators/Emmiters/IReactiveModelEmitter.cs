using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal interface IReactiveModelEmitter
    {
        void Emit(
            INamedTypeSymbol model,
            IndentedTextWriter writer);
    }
}
