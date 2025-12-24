using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using EffectSharp.SourceGenerators.Model;
using EffectSharp.SourceGenerators.Utils;

namespace EffectSharp.SourceGenerators
{
    public sealed partial class ReactiveModelGenerator
    {
        private static ImmutableArray<FunctionCommandSpec> BuildFunctionCommands(INamedTypeSymbol modelSymbol, Compilation compilation, ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var cancellationTokenSymbol = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
            var builder = ImmutableArray.CreateBuilder<FunctionCommandSpec>();
            foreach (var method in modelSymbol.GetMembers().OfType<IMethodSymbol>())
            {
                var attr = SymbolHelper.GetAttribute(method, "FunctionCommand");
                if (attr == null)
                    continue;

                var isAsync = SymbolHelper.IsTaskLike(method.ReturnType, compilation, out var taskResultType);
                var parameters = method.Parameters;

                if (parameters.Length > 2)
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.FunctionCommandTooManyParameters, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                var ctIndex = -1;
                var cmdParamIndex = -1;
                if (isAsync)
                {
                    for (int i = parameters.Length - 1; i >= 0; i--)
                    {
                        if (cancellationTokenSymbol != null && SymbolEqualityComparer.Default.Equals(parameters[i].Type, cancellationTokenSymbol))
                        {
                            ctIndex = i;
                            cmdParamIndex = parameters.Length == 2 ? 1 - i : -1;
                            break;
                        }
                    }
                }
                else
                {
                    cmdParamIndex = parameters.Length == 1 ? 0 : -1;
                }

                var parameterCountWithoutCt = parameters.Length - (ctIndex >= 0 ? 1 : 0);
                if (parameterCountWithoutCt > 1)
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.FunctionCommandTooManyParameters, method.Locations.FirstOrDefault(), method.Name));
                    continue;
                }

                var parameterType = cmdParamIndex >= 0 ? parameters[cmdParamIndex].Type : null;
                var resultType = method.ReturnsVoid ? null : (isAsync ? taskResultType : method.ReturnType);

                var iface = isAsync ? "IAsyncFunctionCommand" : "IFunctionCommand";
                var helper = isAsync ? "CreateFromTask" : "Create";

                var genericArgs = BuildGenericArguments(parameterType, resultType);
                var lambdaParameters = isAsync ? "param, cancellationToken" : "param";
                var callArgs = BuildCommandCallArguments(parameters, cmdParamIndex, ctIndex);

                var canExecute = SymbolHelper.GetNamedArgument<string?>(attr, "CanExecute", null, out _);
                var allowConcurrent = SymbolHelper.GetNamedArgument(attr, "AllowConcurrentExecution", true, out _);
                var scheduler = SymbolHelper.GetNamedArgument(attr, "ExecutionScheduler", string.Empty, out _);
                if (!isAsync && !string.IsNullOrWhiteSpace(scheduler))
                {
                    diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.FunctionCommandSchedulerNonAsync, method.Locations.FirstOrDefault(), method.Name));
                    scheduler = string.Empty;
                }

                var fieldName = "_" + NameHelper.ToCamel(method.Name.EndsWith("Command", StringComparison.Ordinal)
                    ? method.Name.Substring(0, method.Name.Length - "Command".Length)
                    : method.Name + "Command");
                var propertyName = method.Name.EndsWith("Command", StringComparison.Ordinal)
                    ? method.Name.Substring(0, method.Name.Length - "Command".Length)
                    : method.Name + "Command";

                builder.Add(new FunctionCommandSpec(
                    fieldName,
                    propertyName,
                    iface,
                    helper,
                    genericArgs,
                    method.Name,
                    lambdaParameters,
                    callArgs,
                    canExecute,
                    allowConcurrent,
                    scheduler,
                    isAsync));
            }
            return builder.ToImmutable();
        }

        private static string BuildGenericArguments(ITypeSymbol? parameterType, ITypeSymbol? resultType)
        {
            if (resultType != null)
            {
                var paramTypeName = parameterType != null ? parameterType.ToDisplayString(FullName) : "object";
                var resultName = resultType.ToDisplayString(FullName);
                return "<" + paramTypeName + ", " + resultName + ">";
            }
            else
            {
                var paramTypeName = parameterType != null ? parameterType.ToDisplayString(FullName) : "object";
                return "<" + paramTypeName + ">";
            }
        }

        private static string BuildCommandCallArguments(ImmutableArray<IParameterSymbol> parameters, int commandParameterIndex, int cancellationTokenIndex)
        {
            if (parameters.Length == 0)
                return string.Empty;

            var args = new List<string>();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i == commandParameterIndex)
                    args.Add("param");
                else if (i == cancellationTokenIndex)
                    args.Add("cancellationToken");
            }
            return string.Join(", ", args);
        }

        private static void EmitFunctionCommands(ModelSpec spec, IndentedTextWriter iw)
        {
            if (spec.FunctionCommands.Length == 0)
                return;

            foreach (var cmd in spec.FunctionCommands)
            {
                iw.WriteLine("private " + cmd.InterfaceName + cmd.GenericArguments + " " + cmd.FieldName + ";");
                iw.WriteLine("public " + cmd.InterfaceName + cmd.GenericArguments + " " + cmd.PropertyName + " => " + cmd.FieldName + ";");
                iw.WriteLine();
            }
        }

        private static void EmitFunctionCommandInitializers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var cmd in spec.FunctionCommands)
            {
                iw.WriteLine("this." + cmd.FieldName + " = FunctionCommand." + cmd.FactoryMethod + cmd.GenericArguments + "(");
                iw.Indent++;
                iw.Write("(" + cmd.LambdaParameters + ") => this." + cmd.MethodName + "(");
                iw.Write(cmd.CallArguments);
                iw.Write(")");

                if (cmd.HasCanExecute)
                {
                    iw.WriteLine(",");
                    iw.Write("canExecute: " + cmd.CanExecute);
                }
                if (!cmd.AllowConcurrentExecution)
                {
                    iw.WriteLine(",");
                    iw.Write("allowConcurrentExecution: false");
                }
                if (cmd.IsAsync && cmd.HasExecutionScheduler)
                {
                    iw.WriteLine(",");
                    iw.Write("executionScheduler: " + cmd.ExecutionScheduler);
                }
                iw.WriteLine(");");
                iw.Indent--;
                iw.WriteLine();
            }
        }

        private static void EmitFunctionCommandDisposers(ModelSpec spec, IndentedTextWriter iw)
        {
            foreach (var cmd in spec.FunctionCommands)
            {
                iw.WriteLine("this." + cmd.FieldName + "?.Dispose();");
                iw.WriteLine("this." + cmd.FieldName + " = null;");
            }
            if (spec.FunctionCommands.Length > 0)
                iw.WriteLine();
        }
    }
}
