using Microsoft.CodeAnalysis;

namespace EffectSharp.SourceGenerators
{
    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor FunctionCommandTooManyParameters = new(
            id: "EFSP1001",
            title: "FunctionCommand method has too many parameters",
            messageFormat: "Method '{0}' is marked with [FunctionCommand] but has more than one parameter. FunctionCommand methods can have at most one parameter.",
            category: "EffectSharp.FunctionCommand",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor FunctionCommandSchedulerNonAsync = new(
            id: "EFSP1002",
            title: "FunctionCommand Scheduler is only valid for async methods",
            messageFormat: "Method '{0}' is marked with [FunctionCommand] and specifies a Scheduler, but is not an async method. Only async FunctionCommand methods can specify a Scheduler.",
            category: "EffectSharp.FunctionCommand",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor WatchMethodTooManyParameters = new(
            id: "EFSP2001",
            title: "Watch method has too many parameters",
            messageFormat: "Method '{0}' is marked with [Watch] but has more than two parameters. Watch methods can have at most two parameters.",
            category: "EffectSharp.Watch",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor WatchMissingValues = new(
            id: "EFSP2002",
            title: "Watch must specify one or more values",
            messageFormat: "Method '{0}' is marked with [Watch] but does not specify any values",
            category: "EffectSharp.Watch",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ComputedMethodTooManyParameters = new(
            id: "EFSP3001",
            title: "Computed method has parameters",
            messageFormat: "Method '{0}' is marked with [Computed] but has one or more parameters. Computed methods cannot have parameters.",
            category: "EffectSharp.Computed",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ComputedListInvalidReturnType = new(
            id: "EFSP4001",
            title: "ComputedList method must return IList<T>",
            messageFormat: "Method '{0}' is marked with [ComputedList] but does not return a type implementing IList<T>",
            category: "EffectSharp.ComputedList",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ComputedListMethodTooManyParameters = new(
            id: "EFSP4002",
            title: "ComputedList method has parameters",
            messageFormat: "Method '{0}' is marked with [ComputedList] but has one or more parameters. ComputedList methods cannot have parameters.",
            category: "EffectSharp.ComputedList",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DeepTargetMustBeReactive = new(
            id: "EFSP5001",
            title: "[Deep] target must be reactive",
            messageFormat: "Member '{0}' is marked with [Deep] but its type is not reactive and cannot be tracked",
            category: "EffectSharp.Deep",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DeepMethodMustBeComputed = new(
            id: "EFSP5002",
            title: "[Deep] on method requires [Computed] or [ComputedList]",
            messageFormat: "Method '{0}' is marked with [Deep] but is neither [Computed] nor [ComputedList]",
            category: "EffectSharp.Deep",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
