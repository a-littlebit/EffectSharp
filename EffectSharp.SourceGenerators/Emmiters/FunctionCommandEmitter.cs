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
                if (method.Parameters.Length > 1)
                {
                    // Only methods with 0 or 1 parameter are supported
                    context.GeneratorContext.ReportError(
                        Diagnostics.FunctionCommandTooManyParameters,
                        method,
                        method.Name);
                    continue;
                }

                var parameterType = method.Parameters.Length == 1
                    ? method.Parameters[0].Type as INamedTypeSymbol
                    : null;
                var resultType = method.ReturnsVoid
                    ? null
                    : method.ReturnType as INamedTypeSymbol;

                bool isAsync = method.ReturnsTaskLike(context.GeneratorContext.Compilation);

                var genericBuilder = new StringBuilder("<");
                genericBuilder.Append(parameterType == null ? "object" : parameterType.Name);
                if (resultType != null)
                {
                    genericBuilder.Append(", ");
                    genericBuilder.Append(resultType.Name);
                }
                genericBuilder.Append(">");
                var genericTypeArguments = genericBuilder.ToString();
                var fieldName = "_" + NameHelper.ToCamelCase(commandContext.CommandPropertyName);

                commandContext.ParameterType = parameterType;
                commandContext.ResultType = resultType;
                commandContext.IsAsync = isAsync;
                commandContext.GenericTypeArguments = genericTypeArguments;
                commandContext.CanExecuteMethodName = commandContext.AttributeData.GetNamedArgument<string>("CanExecute");

                var interfaceName = commandContext.InterfaceName;
                writer.WriteLine($"private {interfaceName}{genericTypeArguments} {fieldName};");
                writer.WriteLine($"public {interfaceName}{genericTypeArguments} {commandContext.CommandPropertyName} => {fieldName};");
                writer.WriteLine();

                context.RegisterInitializer(EmitInitializer);
            }
        }

        static void EmitInitializer(ReactiveModelContext context, IndentedTextWriter writer)
        {
            foreach (var commandContext in context.FunctionCommands)
            {
                writer.Write($"this.{commandContext.FieldName} = FunctionCommand.{commandContext.HelperFunctionName}{commandContext.GenericTypeArguments}(");

                if (commandContext.ParameterType != null)
                    writer.Write($"(param) => this.{commandContext.Method.Name}(param)");
                else
                    writer.Write($"(_) => this.{commandContext.Method.Name}()");

                if (!string.IsNullOrWhiteSpace(commandContext.CanExecuteMethodName))
                    writer.Write($", () => {commandContext.CanExecuteMethodName}()");

                writer.WriteLine(");");
                writer.WriteLine();
            }
        }
    }
}
