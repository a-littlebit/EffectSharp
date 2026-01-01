; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
EFSP3002 | EffectSharp.Computed | Error | Computed methods must return a value

## Rule Details
#### EFSP3002: Computed methods must return a value
- Category: EffectSharp.Computed
- Severity: Error
- Description: Methods annotated with [Computed] must return a value.
