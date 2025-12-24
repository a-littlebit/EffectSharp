; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
EFSP5001 | EffectSharp.Deep | Error | [Deep] target must be reactive
EFSP5002 | EffectSharp.Deep | Error | [Deep] on method requires [Computed] or [ComputedList]

## Rule Details
#### EFSP5001: [Deep] target must be reactive
- Category: EffectSharp.Deep
- Severity: Error
- Description: The [Deep] attribute can only be applied to properties, fields, computed methods or computed list methods of types that implement IReactive.

#### EFSP5002: [Deep] on method requires [Computed] or [ComputedList]
- Category: EffectSharp.Deep
- Severity: Error
- Description: The [Deep] attribute can only be applied to methods that are also annotated with [Computed] or [ComputedList].
