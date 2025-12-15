using Microsoft.CodeAnalysis;
using System;
using System.Linq;

namespace EffectSharp.SourceGenerators.Utils
{
    internal static class DiagnosticHelper
    {
        public static void Report(
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

        public static void Report(
            this GeneratorExecutionContext context,
            DiagnosticException exception)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    exception.Descriptor,
                    exception.Symbol.Locations.FirstOrDefault(),
                    exception.Args));
        }
    }

    internal class DiagnosticException : Exception
    {
        public DiagnosticDescriptor Descriptor { get; }

        public ISymbol Symbol { get; }

        public object[] Args { get; }

        public DiagnosticException(
            DiagnosticDescriptor descriptor,
            ISymbol symbol,
            params object[] args)
        {
            Descriptor = descriptor;
            Symbol = symbol;
            Args = args;
        }
    }

    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor FunctionCommandTooManyParameters = new(
            id: "ES1001",
            title: "FunctionCommand method has too many parameters",
            messageFormat: "Method '{0}' is marked with [FunctionCommand] but has more than one parameter. FunctionCommand methods can have at most one parameter.",
            category: "EffectSharp.FunctionCommand",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor FunctionCommandSchedulerNonAsync = new(
            id: "ES1002",
            title: "FunctionCommand Scheduler is only valid for async methods",
            messageFormat: "Method '{0}' is marked with [FunctionCommand] and specifies a Scheduler, but is not an async method. Only async FunctionCommand methods can specify a Scheduler.",
            category: "EffectSharp.FunctionCommand",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
