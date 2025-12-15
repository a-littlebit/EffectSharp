; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

# Analyzer Releases - Unshipped

## Upcoming Release

### New Rules

ID | Category | Severity | Title
---|---|---|---
ES1001 | EffectSharp.FunctionCommand | Error | FunctionCommand method has too many parameters
ES1002 | EffectSharp.FunctionCommand | Warning | FunctionCommand Scheduler is only valid for async methods
ES2001 | EffectSharp.Watch | Error | Watch method has too many parameters

#### ES1001: FunctionCommand method has too many parameters
- Category: EffectSharp.FunctionCommand
- Severity: Error
- Description: Methods annotated with [FunctionCommand] can have at most one command input parameter. For async methods, an optional CancellationToken may be used.

#### ES1002: FunctionCommand Scheduler is only valid for async methods
- Category: EffectSharp.FunctionCommand
- Severity: Warning
- Description: The Scheduler (execution scheduler) on [FunctionCommand] is only applicable to asynchronous methods returning Task or Task<T>. Specifying a scheduler on a non-async method will trigger this diagnostic.

#### ES2001: Watch method has too many parameters
- Category: EffectSharp.Watch
- Severity: Error
- Description: Methods annotated with [Watch] can have at most two parameters: the new value and (optionally) the old value.