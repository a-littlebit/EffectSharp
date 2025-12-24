namespace EffectSharp.SourceGenerators.Model
{
    internal readonly struct FunctionCommandSpec
    {
        public readonly string FieldName;
        public readonly string PropertyName;
        public readonly string InterfaceName;
        public readonly string FactoryMethod;
        public readonly string GenericArguments;
        public readonly string MethodName;
        public readonly string LambdaParameters;
        public readonly string CallArguments;
        public readonly string? CanExecute;
        public readonly bool AllowConcurrentExecution;
        public readonly string? ExecutionScheduler;
        public readonly bool IsAsync;

        public bool HasCanExecute => !string.IsNullOrWhiteSpace(CanExecute);
        public bool HasExecutionScheduler => !string.IsNullOrWhiteSpace(ExecutionScheduler);

        public FunctionCommandSpec(
            string fieldName,
            string propertyName,
            string interfaceName,
            string factoryMethod,
            string genericArguments,
            string methodName,
            string lambdaParameters,
            string callArguments,
            string? canExecute,
            bool allowConcurrentExecution,
            string? executionScheduler,
            bool isAsync)
        {
            FieldName = fieldName;
            PropertyName = propertyName;
            InterfaceName = interfaceName;
            FactoryMethod = factoryMethod;
            GenericArguments = genericArguments;
            MethodName = methodName;
            LambdaParameters = lambdaParameters;
            CallArguments = callArguments;
            CanExecute = canExecute;
            AllowConcurrentExecution = allowConcurrentExecution;
            ExecutionScheduler = executionScheduler;
            IsAsync = isAsync;
        }
    }
}
