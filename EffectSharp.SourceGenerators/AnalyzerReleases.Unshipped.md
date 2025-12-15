; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

# Analyzer Releases - Unshipped

## Upcoming Release

### New Rules

- ES1001: FunctionCommand method has too many parameters
  - Category: EffectSharp.FunctionCommand
  - Severity: Error
  - Description: Methods annotated with [FunctionCommand] should not have parameters other than the command's input type and CancellationToken.

- ES1002: FunctionCommand Scheduler is only valid for async methods
  -	Category: EffectSharp.FunctionCommand
  - Severity: Warning
  - Description: The Scheduler property on [FunctionCommand] is only applicable to asynchronous methods returning Task or Task<T>.

- ES1003: Watch method has too many parameters
  - Category: EffectSharp.Watch
  - Severity: Error
  - Description: The Watch method should have at most two parameters.