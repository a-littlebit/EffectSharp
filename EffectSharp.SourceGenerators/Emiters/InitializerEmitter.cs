using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal sealed class InitializerEmitter : IReactiveModelEmitter
    {
        public void RequireTypes(KnownTypeRegistry registry)
        {
            // No specific types required
        }

        public IncrementalValuesProvider<INamedTypeSymbol> Subcribe(
            IncrementalGeneratorInitializationContext context,
            IncrementalValuesProvider<INamedTypeSymbol> modelProvider)
        {
            return modelProvider;
        }

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
