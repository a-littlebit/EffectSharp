using EffectSharp.SourceGenerators.Context;
using EffectSharp.SourceGenerators.Emitters;
using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Emmiters
{
    internal class FunctionCommandEmitter : IReactiveModelEmitter
    {
        public void Emit(ReactiveModelContext context, IndentedTextWriter writer)
        {
            var commandMethods = context.ModelSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Select(m => new FunctionCommandContext(m, m.GetAttributeData<AttributeData>("FunctionCommandAttribute")))
                .Where(m => m.AttributeData != null)
                .ToList();

            context.FunctionCommands = commandMethods;

            foreach (var commandContext in commandMethods)
            {
                var method = commandContext.Method;

                int parameterCount = method.Parameters.Length;
                int commandParameterIndex = 0;
                int cancellationTokenIndex = -1;

                if (parameterCount <= 2)
                {
                    for (int i = method.Parameters.Length - 1; i >= 0; i--)
                    {
                        var parameter = method.Parameters[i];
                        if (parameter.Type.IsCancellationToken(context.GeneratorContext.Compilation))
                        {
                            cancellationTokenIndex = i;
                            commandParameterIndex = 1 - i;
                            parameterCount--;
                            break;
                        }
                    }
                }

                if (commandParameterIndex >= method.Parameters.Length)
                    commandParameterIndex = -1;

                if (parameterCount > 1)
                {
                    // Only methods with 0 or 1 parameter are supported
                    context.GeneratorContext.ReportError(
                        Diagnostics.FunctionCommandTooManyParameters,
                        method,
                        method.Name);
                    continue;
                }

                var parameterType = commandParameterIndex == -1
                    ? null
                    : method.Parameters[commandParameterIndex].Type as INamedTypeSymbol;
                var resultType = method.ReturnsVoid
                    ? null
                    : method.ReturnType as INamedTypeSymbol;

                bool isAsync = method.ReturnsTaskLike(context.GeneratorContext.Compilation, out var taskResultType);
                if (isAsync)
                    resultType = taskResultType;
                var scheduler = commandContext.AttributeData.GetNamedArgument("ExecutionScheduler", "");
                if (!isAsync && !string.IsNullOrWhiteSpace(scheduler))
                {
                    // Scheduler can only be specified for async commands
                    context.GeneratorContext.ReportError(
                        Diagnostics.FunctionCommandSchedulerRequiresAsync,
                        method,
                        method.Name);
                    continue;
                }

                var genericBuilder = new StringBuilder("<");
                genericBuilder.Append(parameterType == null ? "object" : parameterType.ToDisplayString());
                if (resultType != null)
                {
                    genericBuilder.Append(", ");
                    genericBuilder.Append(resultType.ToDisplayString());
                }
                genericBuilder.Append(">");
                var genericTypeArguments = genericBuilder.ToString();
                var fieldName = commandContext.FieldName;

                commandContext.ParameterType = parameterType;
                commandContext.ResultType = resultType;
                commandContext.IsAsync = isAsync;
                commandContext.GenericTypeArguments = genericTypeArguments;
                commandContext.CanExecuteMethodName = commandContext.AttributeData.GetNamedArgument<string>("CanExecute");
                commandContext.CommandParameterIndex = commandParameterIndex;
                commandContext.CancellationTokenParameterIndex = cancellationTokenIndex;
                commandContext.AllowConcurrentExecution = commandContext.AttributeData.GetNamedArgument("AllowConcurrentExecution", true);
                commandContext.ExecutionScheduler = scheduler;

                var interfaceName = commandContext.InterfaceName;
                writer.WriteLine($"private {interfaceName}{genericTypeArguments} {fieldName};");
                writer.WriteLine($"public {interfaceName}{genericTypeArguments} {commandContext.PropertyName} => {fieldName};");
                writer.WriteLine();

                context.RegisterInitializer(EmitInitializer);
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

                writer.Write($") => this.{commandContext.Method.Name}(");

                for (int i = 0; i < commandContext.Method.Parameters.Length; i++)
                {
                    if (i != 0)
                        writer.Write(", ");
                    if (i == commandContext.CommandParameterIndex)
                        writer.Write("param");
                    else if (i == commandContext.CancellationTokenParameterIndex)
                        writer.Write("cancellationToken");
                }

                writer.Write(")");

                if (!string.IsNullOrWhiteSpace(commandContext.CanExecuteMethodName))
                    writer.Write($", () => {commandContext.CanExecuteMethodName}()");

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
