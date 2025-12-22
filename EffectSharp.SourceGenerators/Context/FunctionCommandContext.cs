using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    internal class FunctionCommandContext
    {
        public IMethodSymbol MethodSymbol { get; }

        public bool IsValid { get; }

        public AttributeData? AttributeData { get; }

        public string PropertyName => MethodSymbol.Name.EndsWith("Command") ?
            MethodSymbol.Name.Substring(0, MethodSymbol.Name.Length - "Command".Length) :
            MethodSymbol.Name + "Command";

        public string FieldName => "_" + NameHelper.ToCamelCase(PropertyName);

        public string InterfaceName => IsAsync ? "IAsyncFunctionCommand" : "IFunctionCommand";

        public string HelperFunctionName => IsAsync ? "CreateFromTask" : "Create";

        public bool IsAsync { get; }

        public INamedTypeSymbol? ParameterType { get; }

        public INamedTypeSymbol? ResultType { get; }

        public string GenericTypeArguments =>
            ResultType != null ?
            $"<{(ParameterType != null ? ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : "object")}, {ResultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>" :
            $"<{(ParameterType != null ? ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : "object")}>";

        public int CommandParameterIndex { get; }

        public int CancellationTokenParameterIndex { get; }

        public string? CanExecute { get; set; }

        public bool AllowConcurrentExecution { get; }

        public string? ExecutionScheduler { get; }

        public FunctionCommandContext(IMethodSymbol method, ReactiveModelContext modelContext)
        {
            MethodSymbol = method;

            AttributeData = method.GetAttributeData("FunctionCommand");
            if (AttributeData == null)
                return;

            IsAsync = method.ReturnsTaskLike(modelContext.KnownTypes, out var taskResultType);
            ResultType = method.ReturnsVoid ? null :
                IsAsync ?
                    taskResultType :
                    method.ReturnType as INamedTypeSymbol;

            var cancellationTokenSymbol = modelContext.KnownTypes.Get("System.Threading.CancellationToken");
            var commandParameterIndex = -1;
            var cancellationTokenIndex = -1;
            var paramCount = method.Parameters.Length;

            if (paramCount > 2)
            {
                Report(
                    modelContext,
                    DiagnosticHelper.FunctionCommandTooManyParameters);
                return;
            }

            if (IsAsync)
            {
                for (int i = method.Parameters.Length - 1; i >= 0; i--)
                {
                    var parameter = method.Parameters[i];
                    if (parameter.Type.Equals(cancellationTokenSymbol, SymbolEqualityComparer.Default))
                    {
                        cancellationTokenIndex = i;
                        commandParameterIndex = 1 - i;
                        paramCount--;
                        break;
                    }
                }
            }
            else
            {
                commandParameterIndex = paramCount == 1 ? 0 : -1;
            }

            if (paramCount > 1)
            {
                Report(
                    modelContext,
                    DiagnosticHelper.FunctionCommandTooManyParameters);
                return;
            }

            if (commandParameterIndex >= method.Parameters.Length)
                commandParameterIndex = -1;

            ParameterType = commandParameterIndex == -1
                ? null
                : method.Parameters[commandParameterIndex].Type as INamedTypeSymbol;

            CommandParameterIndex = commandParameterIndex;
            CancellationTokenParameterIndex = cancellationTokenIndex;

            CanExecute = AttributeData.GetNamedArgument<string>("CanExecute");

            AllowConcurrentExecution = AttributeData.GetNamedArgument("AllowConcurrentExecution", true);

            var scheduler = AttributeData.GetNamedArgument("ExecutionScheduler", "");
            if (!IsAsync && !string.IsNullOrWhiteSpace(scheduler))
            {
                // Scheduler can only be specified for async commands
                Report(
                    modelContext,
                    DiagnosticHelper.FunctionCommandSchedulerNonAsync);
                scheduler = "";
            }

            ExecutionScheduler = scheduler;

            IsValid = true;
        }

        private void Report(ReactiveModelContext context, DiagnosticDescriptor descriptor)
        {
            context.ProductionContext.Report(
                descriptor,
                MethodSymbol,
                MethodSymbol.Name);
        }
    }
}
