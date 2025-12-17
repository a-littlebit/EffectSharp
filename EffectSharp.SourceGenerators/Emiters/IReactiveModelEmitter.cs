using EffectSharp.SourceGenerators.Context;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal interface IReactiveModelEmitter
    {
        void Emit(
            ReactiveModelContext context,
            IndentedTextWriter writer);
    }
}
