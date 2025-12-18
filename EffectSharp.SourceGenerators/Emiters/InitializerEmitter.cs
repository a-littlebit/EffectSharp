using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal sealed class InitializerEmitter : IReactiveModelEmitter
    {
        public void Emit(ReactiveModelContext context, IndentedTextWriter writer)
        {
            writer.WriteLine("public void InitializeReactiveModel()");
            writer.WriteLine("{");
            writer.Indent++;

            context.EmitInitializers(writer);

            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();

            writer.WriteLine("public void DisposeReactiveModel()");
            writer.WriteLine("{");
            writer.Indent++;

            context.EmitDisposers(writer);

            writer.Indent--;
            writer.WriteLine("}");
        }
    }
}
