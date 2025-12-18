using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal class FunctionCommandEmitter : IReactiveModelEmitter
    {
        public void Emit(ReactiveModelContext context, IndentedTextWriter writer)
        {
            context.FunctionCommands = context.ModelSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new FunctionCommandContext(m, context))
                .Where(m => m.IsValid)
                .ToList();

            EmitDefinition(context, writer);
            context.RegisterInitializer(EmitInitializer);
        }

        static void EmitDefinition(ReactiveModelContext context, IndentedTextWriter writer)
        {
            foreach (var commandContext in context.FunctionCommands)
            {
                var interfaceName = commandContext.InterfaceName;
                var genericTypeArguments = commandContext.GenericTypeArguments;
                var fieldName = commandContext.FieldName;
                writer.WriteLine($"private {interfaceName}{genericTypeArguments} {fieldName};");
                writer.WriteLine($"public {interfaceName}{genericTypeArguments} {commandContext.PropertyName} => {fieldName};");
                writer.WriteLine();
            }
        }

        static void EmitInitializer(ReactiveModelContext context, IndentedTextWriter writer)
        {
            foreach (var commandContext in context.FunctionCommands)
            {
                writer.Write($"this.{commandContext.FieldName} = FunctionCommand." +
                    $"{commandContext.HelperFunctionName}{commandContext.GenericTypeArguments}((param");

                if (commandContext.IsAsync)
                    writer.Write(", cancellationToken");

                writer.Write($") => this.{commandContext.MethodSymbol.Name}(");

                for (int i = 0; i < commandContext.MethodSymbol.Parameters.Length; i++)
                {
                    if (i != 0)
                        writer.Write(", ");
                    if (i == commandContext.CommandParameterIndex)
                        writer.Write("param");
                    else if (i == commandContext.CancellationTokenParameterIndex)
                        writer.Write("cancellationToken");
                }

                writer.Write(")");

                if (!string.IsNullOrWhiteSpace(commandContext.CanExecute))
                    writer.Write($", canExecute: {commandContext.CanExecute}");

                if (!commandContext.AllowConcurrentExecution)
                    writer.Write(", allowConcurrentExecution: false");

                if (commandContext.IsAsync && !string.IsNullOrWhiteSpace(commandContext.ExecutionScheduler))
                    writer.Write($", executionScheduler: {commandContext.ExecutionScheduler}");

                writer.WriteLine(");");
                writer.WriteLine();
            }
        }
    }
}
