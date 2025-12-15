using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class Diagnostics
    {
        public static void ReportError(
            this GeneratorExecutionContext context,
            DiagnosticDescriptor descriptor,
            ISymbol symbol,
            params object[] args)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    descriptor,
                    symbol.Locations.FirstOrDefault(),
                    args));
        }


        public static readonly DiagnosticDescriptor FunctionCommandTooManyParameters = new(
            id: "ES1001",
            title: "FunctionCommand method has too many parameters",
            messageFormat: "Method '{0}' is marked with [FunctionCommand] but has more than one parameter. FunctionCommand methods can have at most one parameter.",
            category: "EffectSharp.FunctionCommand",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor FunctionCommandSchedulerRequiresAsync = new(
            id: "ES1002",
            title: "FunctionCommand Scheduler requires async method",
            messageFormat: "Method '{0}' is marked with [FunctionCommand] and specifies a Scheduler, but is not an async method. Only async FunctionCommand methods can specify a Scheduler.",
            category: "EffectSharp.FunctionCommand",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
