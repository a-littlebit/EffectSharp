using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Emitters
{
    internal class FunctionCommandEmitter : IReactiveModelEmitter
    {
        public void RequireTypes(KnownTypeRegistry registry)
        {
            registry.Require("System.Threading.CancellationToken");
        }

        public void Emit(ReactiveModelContext context, IndentedTextWriter writer)
        {
            context.FunctionCommands = context.ModelSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new FunctionCommandContext(m, context))
                .Where(m => m.IsValid)
                .ToList();

            foreach (var commandContext in context.FunctionCommands)
            {
                EmitDefinition(commandContext, writer);
            }
            context.RegisterInitializer(EmitInitializer);
            context.RegisterDisposer(EmitDisposer);
        }

        static void EmitDefinition(FunctionCommandContext commandContext, IndentedTextWriter writer)
        {
            var interfaceName = commandContext.InterfaceName;
            var genericTypeArguments = commandContext.GenericTypeArguments;
            var fieldName = commandContext.FieldName;
            writer.WriteLine($"private {interfaceName}{genericTypeArguments} {fieldName};");
            writer.WriteLine($"public {interfaceName}{genericTypeArguments} {commandContext.PropertyName} => {fieldName};");
            writer.WriteLine();
        }

        static void EmitInitializer(ReactiveModelContext context, IndentedTextWriter writer)
        {
            foreach (var commandContext in context.FunctionCommands)
            {
                writer.WriteLine($"this.{commandContext.FieldName} = FunctionCommand." +
                    $"{commandContext.HelperFunctionName}{commandContext.GenericTypeArguments}(");

                writer.Indent++;
                writer.Write("(param");

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

                void WriteOption(string name, string value)
                {
                    writer.WriteLine(",");
                    writer.Write($"{name}: {value}");
                }

                if (!string.IsNullOrWhiteSpace(commandContext.CanExecute))
                    WriteOption("canExecute", commandContext.CanExecute);

                if (!commandContext.AllowConcurrentExecution)
                    WriteOption("allowConcurrentExecution", "false");

                if (commandContext.IsAsync && !string.IsNullOrWhiteSpace(commandContext.ExecutionScheduler))
                    WriteOption("executionScheduler", commandContext.ExecutionScheduler);

                writer.WriteLine(");");
                writer.Indent--;

                writer.WriteLine();
            }
        }

        static void EmitDisposer(ReactiveModelContext modelContext, IndentedTextWriter iw)
        {
            foreach (var commandContext in modelContext.FunctionCommands)
            {
                iw.WriteLine($"this.{commandContext.FieldName}?.Dispose();");
                iw.WriteLine($"this.{commandContext.FieldName} = null;");
                iw.WriteLine();
            }
        }
    }
}
