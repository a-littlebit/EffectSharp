; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.3.0

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------

## Rule Details

#### EFSP1001: FunctionCommand method has too many parameters
- Category: EffectSharp.FunctionCommand
- Severity: Error
- Description: Methods annotated with [FunctionCommand] can have at most one command input parameter. For async methods, an optional CancellationToken may be used.

#### EFSP1002: FunctionCommand Scheduler is only valid for async methods
- Category: EffectSharp.FunctionCommand
- Severity: Warning
- Description: The Scheduler (execution scheduler) on [FunctionCommand] is only applicable to asynchronous methods returning Task or Task<T>. Specifying a scheduler on a non-async method will trigger this diagnostic.

#### EFSP2001: Watch method has too many parameters
- Category: EffectSharp.Watch
- Severity: Error
- Description: Methods annotated with [Watch] can have at most two parameters: the new value and (optionally) the old value.

#### EFSP3001: Computed method has too many parameters
- Category: EffectSharp.Computed
- Severity: Error
- Description: Methods annotated with [Computed] cannot have any parameters.
