using EffectSharp.SourceGenerators.Utils;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace EffectSharp.SourceGenerators.Context
{
    internal class FunctionCommandContext
    {
        public IMethodSymbol Method { get; }

        public string PropertyName => Method.Name.EndsWith("Command") ?
            Method.Name.Substring(0, Method.Name.Length - "Command".Length) :
            Method.Name + "Command";

        public string FieldName => "_" + NameHelper.ToCamelCase(PropertyName);

        public string InterfaceName => IsAsync ? "IAsyncFunctionCommand" : "IFunctionCommand";

        public string HelperFunctionName => IsAsync ? "CreateFromTask" : "Create";

        public AttributeData AttributeData { get; }

        public INamedTypeSymbol ParameterType { get; set; }

        public INamedTypeSymbol ResultType { get; set; }

        public bool IsAsync { get; set; }

        public string GenericTypeArguments { get; set; }

        public string CanExecuteMethodName { get; set; }

        public int CommandParameterIndex { get; set; } = -1;

        public int CancellationTokenParameterIndex { get; set; } = -1;

        public bool AllowConcurrentExecution { get; set; } = true;

        public string ExecutionScheduler { get; set; } = "";

        public FunctionCommandContext(IMethodSymbol method, AttributeData attributeData)
        {
            Method = method;
            AttributeData = attributeData;
        }
    }
}
