# EffectSharp.SourceGenerators

Incremental Roslyn source generator for EffectSharp that turns simple C# classes into reactive view models using attributes. It generates:

- Reactive properties from fields annotated with `[ReactiveField]`
- Read-only computed properties from methods annotated with `[Computed]`
- Command properties from methods annotated with `[FunctionCommand]`
- Watch/effect subscriptions from methods annotated with `[Watch]`
- Boilerplate: `INotifyPropertyChanging`, `INotifyPropertyChanged`, `IReactive`, `InitializeReactiveModel()` and `TrackDeep()`

This project is implemented as an Incremental Generator for fast, scalable builds and a smooth IDE experience.

## Installation

Add package references to your project:

```xml
<ItemGroup>
  <PackageReference Include="EffectSharp" Version="<latest>" />
  <PackageReference Include="EffectSharp.SourceGenerators" Version="<latest>" PrivateAssets="all" />
</ItemGroup>
```

Notes:
- `PrivateAssets="all"` prevents the generator from flowing transitively to consumers of your library.
- Works with SDK-style projects using .NET 5+ (source generator support). The generator itself targets `netstandard2.0`.

## Quick Start

Annotate your view model with attributes and call the generated initializer in the constructor:

```csharp
using EffectSharp;
using EffectSharp.SourceGenerators;

[ReactiveModel]
public partial class MainViewModel
{
    [ReactiveField]
    private int _count = 0;

    [ReactiveField]
    private int _restoreCount = 0;

    [Computed]
    public int CurrentCount() => Count + RestoreCount; // => property: ComputedCurrentCount

    [Computed]
    public string ComputeDisplayCount() => $"Current Count: {ComputedCurrentCount}"; // => property: DisplayCount

    [Watch(Properties = new[] { nameof(Count) })]
    public void OnDisplayCountChanged(int newCount, int oldCount)
    {
        // runs whenever Count changes
    }

    [FunctionCommand(CanExecute = nameof(CanIncrement), AllowConcurrentExecution = false)]
    public async Task Increment()
    {
        Count++;
        await Task.Delay(200).ConfigureAwait(false);
    }

    public bool CanIncrement() => Count < 10;

    public MainViewModel()
    {
        InitializeReactiveModel(); // required: wires up computed values and watchers
    }
}
```

What gets generated (conceptually):
- `public int Count { get; set; }` with `PropertyChanging/Changed` notification and reactive dependency tracking
- `public int RestoreCount { get; set; }`
- `public int ComputedCurrentCount { get; }` computed from `CurrentCount()`
- `public string DisplayCount { get; }` computed from `ComputeDisplayCount()`
- `public IAsyncFunctionCommand<object> IncrementCommand { get; }`
- `public void InitializeReactiveModel()` that creates computed values, subscribes watchers, and hooks change notifications
- `public void TrackDeep()` to propagate tracking to nested `IReactive` values

## Attributes

### `[ReactiveModel]` (class)
Marks a partial class as a reactive model. The generator adds interfaces and generated members to this partial type.

### `[ReactiveField]` (field)
Generates a reactive property for the field.

Options:
- `EqualsMethod` (string): custom equality method used to short-circuit unchanged sets.
  - Must be a fully resolvable callable returning `bool` with signature compatible with `(oldValue, newValue)`.
  - Default is `EqualityComparer<T>.Default`.

Example:
```csharp
[ReactiveField(EqualsMethod = "System.Collections.Generic.EqualityComparer<T>.Default")]
private string _name;
```

### `[Computed]` (method)
Generates a read-only property computed from the method.

Naming:
- If the method name starts with `Compute`, the property name is the method name without `Compute` (e.g., `ComputeTotal` -> `Total`).
- Otherwise the property name is prefixed with `Computed` (e.g., `Total` -> `ComputedTotal`).

Options:
- `SetterMethod` (string): optional setter callback invoked when the computed value is assigned via the generated `Computed<T>` wrapper.

### `[FunctionCommand]` (method)
Generates a command property exposing either `IFunctionCommand<T>` or `IAsyncFunctionCommand<T>` depending on the method signature.

Supported method shapes:
- Sync: `void Method(TParam param)` or `void Method()`
- Async: `Task Method(TParam param, CancellationToken ct)` or `Task Method()`

Options:
- `CanExecute` (string): name of a parameterless method returning `bool` used for `CanExecute`.
- `AllowConcurrentExecution` (bool, default `true`): set to `false` to serialize command executions.
- `ExecutionScheduler` (string): scheduler expression used only for async commands.

### `[Watch]` (method)
Creates an effect that re-runs when the specified properties change.

Options:
- `Properties` (string[]): names of properties to watch; more than one creates a tuple `(p1, p2, ...)`.
- `Options` (string): expression for `WatchOptions` (may be `null`).

Supported method shapes:
- `(newValue)` or `(newValue, oldValue)`

## Diagnostics

The generator ships analyzers that validate attribute usage. Unshipped diagnostics:

- ES1001 (Error): FunctionCommand method has too many parameters
- ES1002 (Warning): FunctionCommand Scheduler is only valid for async methods
- ES2001 (Error): Watch method has too many parameters

See `AnalyzerReleases.Unshipped.md` and `AnalyzerReleases.Shipped.md` for release tracking.

## Build and Try

Build the solution:

```bash
# from the repository root
 dotnet build -c Debug
```

Run the WPF counter example (Windows):

```bash
 dotnet build Examples/Example.Wpf.Counter/Example.Wpf.Counter.csproj -c Debug
```

Inspect generated sources (optional):

```xml
<!-- Add to your consuming project to view generated files -->
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files will appear under `obj/generated/`. Look for `*.Reactive.g.cs` next to your annotated types.

## Notes

- Always invoke `InitializeReactiveModel()` once (e.g., in the constructor) to initialize computed values and watchers.
- The generator implements `TrackDeep()` which calls `TrackDeep()` on nested `IReactive` members to propagate dependency tracking.
- This project is an Incremental Generator, making it efficient for large solutions and responsive in the IDE.
