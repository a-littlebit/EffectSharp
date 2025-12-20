# EffectSharp.SourceGenerators

Incremental Roslyn source generator for EffectSharp that turns simple C# classes into reactive view models using attributes. It generates:

- Reactive properties from fields annotated with `[ReactiveField]`
- Read-only computed properties from methods annotated with `[Computed]`
- `ReactiveCollection<T>` computed from methods annotated with `[ComputedList]` with minimal updates
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
public partial class CounterViewModel : IDisposable
{
    [ReactiveField]
    private int _count = 0;

    [FunctionCommand]
    public void Increment() => Count++; // Generated property

    [FunctionCommand(CanExecute = nameof(CanDecrement))]
    public void Decrement() => Count--;
    
    public bool CanDecrement() => Count > 0;

    [Computed]
    public string ComputeDisplayCount() => $"Current Count: {Count}";

    [ReactiveField(EqualsMethod = null)] // disable equality check
    private int _maxCount = 0;

    private ReactiveCollection<(int Count, DateTime Timestamp)> _records = new();

    [Watch(Values =  [nameof(Count)])]
    private void OnCountChanged(int newCount, int oldCount)
    {
        if (MaxCount < newCount)
        {
            MaxCount = newCount;
            records.Add((newCount, DateTime.Now));
        }
    }

    [ReactiveField]
    private bool _orderByCount = true;

    [ComputedList(KeySelector = "x => x.Count")]
    public List<(int Count, DateTime Timestamp)> CurrentRecords()
    {
        if (OrderByCount)
        {
            return records.OrderBy(r => r.Count).ToList();
        }
        else
        {
            return records.OrderBy(r => r.Timestamp).ToList();
        }
    }

    public CounterViewModel()
    {
        InitializeReactiveModel(); // Generated method
    }

    public void Dispose()
    {
        DisposeReactiveModel(); // Generated method
    }
}
```

What gets generated (conceptually):
- Implementation of `INotifyPropertyChanging` and `INotifyPropertyChanged`
- Implementation of `IReactive` and `TrackDeep()` method for dependency tracking
- `public void InitializeReactiveModel()` that creates computed values, subscribes watchers, and hooks change notifications
- `public void DisposeReactiveModel()` that disposes computed values and watchers
- `public int Count { get; set; }` with `PropertyChanging/Changed` notification and reactive dependency tracking
- `public int IFunctionCommand<object> IncrementCommand { get; }` for the `Increment` method with `CanExecute` support
- `public string DisplayCount { get; }` computed from `ComputeDisplayCount()`
- `public int MaxCount { get; set; }` with no equality check on set
- `Reactive.Watch` subscription for `OnCountChanged` in `InitializeReactiveModel()`
- `public bool OrderByCount { get; set; }` with notification
- `public ReactiveCollection<(int Count, DateTime Timestamp)> CurrentRecords { get; }` computed list from `CurrentRecords()` method with minimal updates.

## Attributes

### `[ReactiveModel]` (class)
Marks a partial class as a reactive model. The generator adds interfaces and generated members to this partial type
with an `InitializeReactiveModel()` method to wire up reactive properties, computed values, commands, and watchers
and a `DisposeReactiveModel()` method to release resources.

### `[ReactiveField]` (field)
Generates a reactive property for the field.

Options:
- `EqualsMethod` (string): custom equality method used to short-circuit unchanged sets.
  - Must be a fully resolvable callable returning `bool` with signature compatible with `(oldValue, newValue)`.
  - Default is `EqualityComparer<T>.Default`.

Example:
```csharp
[ReactiveField(EqualsMethod = "MyEqualityComparer.Equals")]
private string _name;
```

### `[Computed]` (method)
Generates a read-only property computed from the method.

Naming:
- If the method name starts with `Compute`, the property name is the method name without `Compute` (e.g., `ComputeTotal` -> `Total`).
- Otherwise the property name is prefixed with `Computed` (e.g., `Total` -> `ComputedTotal`).

Options:
- `Setter` (string): optional setter callback invoked when the computed value is assigned via the generated `Computed<T>` wrapper.

### `[ComputedList]` (method)
Generates a read-only `ReactiveCollection<T>` property computed from the method returning an `IList<T>`.

Naming:
- If the method name starts with `Compute`, the property name is the method name without `Compute` (e.g., `ComputeItems` -> `Items`).
- Otherwise the property name is prefixed with `Computed` (e.g., `Items` -> `ComputedItems`).

Options:
- `KeySelector` (string): optional key selector expression used to identify items for minimal updates.
  - Must be a fully resolvable expression of type `Func<T, TKey>`.
  - If not provided, items are compared by reference.
- `EqualityComparer` (string): optional equality comparer expression used to compare items for minimal updates.
  - Must be a fully resolvable expression of type `EqualityComparer<T>`.
  - If not provided, `EqualityComparer<T>.Default` is used.

### `[FunctionCommand]` (method)
Generates a command property exposing either `IFunctionCommand` or `IAsyncFunctionCommand` depending on the method signature.

Supported method shapes:
- Sync: `TResult Method(TParam param)` or `void Method()`
- Async: `async Task<TResult> Method(TParam param, CancellationToken ct)` or `Task Method()`

Options:
- `CanExecute` (string): expression of a parameterless method returning `bool` used for `CanExecute`.
- `AllowConcurrentExecution` (bool, default `true`): set to `false` to serialize command executions.
- `ExecutionScheduler` (string): scheduler expression used only for async commands.

### `[Watch]` (method)
Creates an effect that re-runs when the specified properties change.

Options:
- `Values` (string[]): array of value expressions to watch; more than one creates a tuple `(v1, v2, ...)`.
- `Immediate` (bool, default `false`): if `true`, runs the watcher immediately upon initialization.
- `Deep` (bool, default `false`): if `true`, tracks deep changes on `IReactive` properties.
- `Once` (bool, default `false`): if `true`, runs the watcher only once when any of the values change.
- `Scheduler` (string): `Action<Effect>` scheduler expression to schedule watcher execution.
- `SupressEquality` (bool, default `true`): if `true`, the watcher will not run if the new and old values are equal.
- `EqualityComparer` (string, default `null`): `EqualityComparer<T>` expression used to compare new and old values when `SupressEquality` is `true`.

Supported method shapes:
- `()`, `(newValue)` or `(newValue, oldValue)`
- When multiple values are watched, use a tuple for the value parameters.
- 
## Resource Disposal

The generator emits a unified disposal method to release reactive resources created during initialization:

- **Generated method:** `public void DisposeReactiveModel()`
- **What it does:**
  - Disposes all generated `Computed<T>` instances and `Watch` effects (`Effect`) created in `InitializeReactiveModel()`.
  - Clears corresponding backing fields to `null` for idempotent repeated calls.
- **When to call:**
  - When the reactive model is no longer needed (e.g., view deactivation, window closing).
  - Typical integration points: implement `IDisposable` on your view model and call `DisposeReactiveModel()` from `Dispose()`, or call it from lifecycle hooks.
- **Idempotency:**
  - Safe to call multiple times; calling `DisposeReactiveModel()` after `InitializeReactiveModel()` will release resources. If you need the model again, call `InitializeReactiveModel()` to re-wire computations and watchers.

## Diagnostics

The generator ships analyzers that validate attribute usage.

See [AnalyzerReleases.Unshipped.md](AnalyzerReleases.Unshipped.md) and [AnalyzerReleases.Shipped.md](AnalyzerReleases.Shipped.md) for release tracking.

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

Generated files will appear under `obj/generated/`. Look for `*.ReactiveModel.g.cs` next to your annotated types.

## Notes

- Always invoke `InitializeReactiveModel()` once (e.g., in the constructor) to initialize computed values and watchers.
- The generator implements `TrackDeep()` which calls `TrackDeep()` on nested `IReactive` members to propagate dependency tracking.
- This project is an Incremental Generator, making it efficient for large solutions and responsive in the IDE.
