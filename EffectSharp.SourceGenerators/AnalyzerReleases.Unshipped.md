; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
EFSP1001 | EffectSharp.FunctionCommand | Error | FunctionCommand method has too many parameters
EFSP1002 | EffectSharp.FunctionCommand | Warning | FunctionCommand Scheduler is only valid for async methods
EFSP2001 | EffectSharp.Watch | Error | Watch method has too many parameters
EFSP3001 | EffectSharp.Computed | Error | Computed method has too many parameters
EFSP4001 | EffectSharp.ComputedList | Error | ComputedList has invalid return type

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

#### EFSP4001: ComputedList has invalid return type
- Category: EffectSharp.ComputedList
- Security: Error
- Description: Methods annotated with [ComputedList] must return a type assignable to IList<T>.
