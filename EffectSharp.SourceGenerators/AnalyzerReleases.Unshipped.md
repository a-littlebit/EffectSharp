; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|--------------------
EFSP2002 | EffectSharp.Watch | Error | Watch must specify one or more values
EFSP4002 | EffectSharp.ComputedList | Error | ComputedList method has parameters
EFSP5001 | EffectSharp.Deep | Error | [Deep] target must be reactive
EFSP5002 | EffectSharp.Deep | Error | [Deep] on method requires [Computed] or [ComputedList]

## Rule Details
#### EFSP2002: Watch must specify one or more values
- Category: EffectSharp.Watch
- Severity: Error
- Description: The [Watch] attribute must specify at least one value to watch.

#### EFSP4002: ComputedList method has parameters
- Category: EffectSharp.ComputedList
- Severity: Error
- Description: Methods annotated with [ComputedList] cannot have any parameters.

#### EFSP5001: [Deep] target must be reactive
- Category: EffectSharp.Deep
- Severity: Error
- Description: The [Deep] attribute can only be applied to properties, fields, computed methods or computed list methods of types that implement IReactive.

#### EFSP5002: [Deep] on method requires [Computed] or [ComputedList]
- Category: EffectSharp.Deep
- Severity: Error
- Description: The [Deep] attribute can only be applied to methods that are also annotated with [Computed] or [ComputedList].
