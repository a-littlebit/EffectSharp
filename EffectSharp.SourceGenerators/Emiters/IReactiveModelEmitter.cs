using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System.CodeDom.Compiler;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal interface IReactiveModelEmitter
    {
        void RequireTypes(KnownTypeRegistry registry);

        void Emit(
            ReactiveModelContext context,
            IndentedTextWriter writer);
    }
}
