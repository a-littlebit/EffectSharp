using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class DiagnosticHelper
    {
        public static void Report(
            this SourceProductionContext context,
            DiagnosticDescriptor descriptor,
            ISymbol symbol,
            params object[] messageArgs)
        {
            var diagnostic = Diagnostic.Create(descriptor, symbol?.Locations.FirstOrDefault(), messageArgs);
            context.ReportDiagnostic(diagnostic);
        }

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
    }
}
