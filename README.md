# EffectSharp

> Lightweight reactive state management for .NET with a Vue 3 inspired API.

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
    - `Ref<T>`
    - Reactive Objects (`Reactive.Create`)
    - Computed Values (`Reactive.Computed`)
    - Effects (`Reactive.Effect`)
    - Watching Changes (`Reactive.Watch`)
    - Reactive Collections (`Reactive.Collection`)
    - Reactive Dictionaries (`Reactive.Dictionary`)
    - Commands (`FunctionCommand`)
- [Usage Examples](#usage-examples)
- [Source Generators](#source-generators)
- [Advanced Topics](#advanced-topics)
    - Notification Batching & `TaskManager` configuration
    - Custom Effect Schedulers
    - Deep Watching
    - List Diff & Binding (`BindTo` / `ComputedList`)
- [Comparison with Vue 3](#comparison-with-vue-3)
- [Limitations](#limitations)
- [FAQ](#faq)
- [License](#license)

## Overview
EffectSharp brings a fine-grained reactive system to .NET similar to Vue 3's reactivity core. Instead of heavy global state containers, you compose reactive primitives that automatically track dependencies and propagate updates efficiently.

It focuses on:
- Minimal Ceremony: Opt-in reactivity with clear primitives.
- Predictable Updates: Dependency tracking via transparent property access.
- Performance: Computed values are lazily evaluated and cached until invalidated.
- UI Friendly: Batched `INotifyPropertyChanged` events reduce UI churn (e.g., WPF, MAUI).

## Key Features
- `Ref<T>` primitive for single mutable reactive values.
- Reactive proxies generated via `DispatchProxy` for interfaces or classes, using `ReactivePropertyAttribute` to control reactivity and deep behavior per property.
- Dependency-tracked computed values (`Computed<T>`) with lazy evaluation & caching.
- Effects that auto re-run when dependencies change; optional scheduling; `Untracked` helpers.
- Flexible watchers: `Ref<T>`, computed getter functions, tuples and complex shapes, deep tracking.
- Reactive `ObservableCollection<T>` enhancement with per-index and list-level dependencies.
- Reactive dictionaries with per-key tracking and key-set dependency.
- LIS-based diff & binding: bind source lists to `ObservableCollection<T>` with minimal updates.
- Unified batching via `TaskManager`; `Reactive.NextTick()` awaits both effect and notify cycles.
- Command helpers (`FunctionCommand`) for synchronous/async commands with `CanExecute` tracking, execution counting, failure events, and optional concurrency control.

## Quick Start

### Create a new reactive object, ref, computed value, and watch changes:
```csharp
using EffectSharp;

// Reactive object proxy
public interface IProduct
{
    [ReactiveProperty(defaultValue: "")]
    string Name { get; set; }

    int Price { get; set; }
}

var product = Reactive.Create<IProduct>();
product.Name = "Laptop";
product.Price = 1000;

// Ref primitive
var count = Reactive.Ref(1);

// Computed value
var totalPrice = Reactive.Computed(() => product.Price * count.Value);

// Watch changes
var watcher = Reactive.Watch(() => totalPrice.Value, (newVal, oldVal) => {
    Console.WriteLine($"Total price changed from {oldVal} to {newVal}");
}, immediate: true); // Total price changed from 0 to 1000
count.Value = 2;
await Reactive.NextTick(); // Total price changed from 1000 to 2000

watcher.Dispose();
```

### Bind to UI (WPF example)
```csharp
using EffectSharp;

// ViewModel with reactive properties
public class MyViewModel
{
    public Ref<int> Counter { get; } = Reactive.Ref(0);
    public Computed<string> DisplayText { get; }
    
    public IFunctionCommand<object> IncrementCommand { get; }
    public IFunctionCommand<object> DecrementCommand { get; }

    public MyViewModel()
    {
        DisplayText = Reactive.Computed(() => $"Counter: {Counter.Value}");

        IncrementCommand = FunctionCommand.Create<object>(_ => Counter.Value++);
        DecrementCommand = FunctionCommand.Create<object>(_ => Counter.Value--);
    }
}
```

```xml
<!-- In XAML, bind to ViewModel properties -->
<Button Command="{Binding IncrementCommand}" Content="+" />
<TextBlock Text="{Binding DisplayText.Value}" />
<Button Command="{Binding DecrementCommand}" Content="-" />
```

## Core Concepts
### `Ref<T>`
A lightweight holder for a single reactive value. Accessing `Value` from inside effects/computed getters tracks the dependency; assigning new values triggers subscribed effects.
```csharp
var counter = Reactive.Ref(0);
Reactive.Watch(() => counter.Value, (newVal, oldVal) => {
    Console.WriteLine($"Counter changed from {oldVal} to {newVal}");
});
counter.Value++;
await Reactive.NextTick(); // Counter changed from 0 to 1
```

### Reactive Objects (`Reactive.Create`)
Create proxies via `DispatchProxy` using interfaces. Use `ReactivePropertyAttribute` to control per-property behavior:
- `reactive: true|false` — whether property changes are tracked and notify.
- `deep: true|false` — whether nested objects are treated reactively.
- `defaultValue` — optional default value for initial state.
```csharp
public interface IOrder
{
    [ReactiveProperty(defaultValue: 1)]
    int Quantity { get; set; }
    [ReactiveProperty(deep: true)]
    IProduct Product { get; set; }
}

var order = Reactive.Create<IOrder>();
order.Product.Name = "Phone";
order.Product.Price = 500;
Reactive.Watch(() => order.Product.Price, (newVal, oldVal) => {
    Console.WriteLine($"Product price changed from {oldVal} to {newVal}");
});
order.Product.Price = 600;
await Reactive.NextTick(); // Product price changed from 500 to 600

// optional: specify target instance
public class MyOrder : IOrder
{
    public int Quantity { get; set; } = 2;
    public IProduct Product { get; set; } = Reactive.Create<IProduct>();
}

var myOrder = Reactive.Create<IOrder>(new MyOrder());
console.WriteLine(myOrder.Quantity); // 2
```

### Computed Values (`Reactive.Computed`)
Lazy, cached derivations. Recomputed only when one of the dependencies accessed during its last evaluation changes.
```csharp
var product = Reactive.Create<IProduct>();
product.Name = "Tablet";
product.Price = 300;
var priceWithTax = Reactive.Computed(() => product.Price + (int)(product.Price * 0.1)); // nothing computed yet
Console.WriteLine(priceWithTax.Value); // 330 (first computation)
product.Price = 400;
Console.WriteLine(priceWithTax.Value); // 440 (recomputed)
```

### Effects (`Reactive.Effect`)
Encapsulate reactive logic. Any dependency read during its execution registers it as a subscriber. Supports custom scheduler for throttling/coalescing and `Untracked` helpers to perform side effects without capturing dependencies.
```csharp
var effect = Reactive.Effect(() => {
    Console.WriteLine($"Product price is now {product.Price}"); // Track dependency
    Effect.Untracked(() => {
        // access without tracking `updateTime`
        Console.WriteLine($"Updated at {updateTime.Value}");
    });
});
product.Price = 500; // triggers effect
await Reactive.NextTick();
updateTime.Value = DateTime.Now; // does not trigger effect
// dispose to stop tracking
effect.Dispose();
```

### Watching Changes (`Reactive.Watch`)
`Watch` lets you observe specific sources without executing a full effect body manually. It returns an `Effect` you can dispose to stop watching. Variants:
- Ref-based: `Watch(refObj, (newVal, oldVal) => ...)`
- Getter-based: `Watch(() => (refA.Value, refB.Value), callback)`; supports tuples and enumerable shapes
- Options: `immediate`, `deep`, `once`, `equalityComparer`, `scheduler`
```csharp
var refA = Reactive.Ref(1);
var refB = Reactive.Ref(10);
var watcher = Reactive.Watch(() => (refA.Value, refB.Value), (_, _) => Console.WriteLine("Either changed"));
refA.Value = 2; // triggers
refB.Value = 20; // triggers
await Reactive.NextTick();
watcher.Dispose();
```

### Reactive Collections (`Reactive.Collection<T>`) and Dictionaries (`Reactive.Dictionary<TKey, TValue>`)
`ReactiveCollection<T>` extends `ObservableCollection<T>` with tracked dependencies for per-index access and overall list changes.
```csharp
var list = Reactive.Collection([10, 20, 30]);
var first = Reactive.Computed(() => list[0]);
list[0] = 100; // invalidates first
Console.WriteLine(first.Value); // 100
```

`ReactiveDictionary<TKey, TValue>` implements `IDictionary<TKey, TValue>` with tracked dependencies for individual keys and the overall key set.

```csharp
var dict = Reactive.Dictionary<string, int>();
var fooValue = Reactive.Computed(() => dict.ContainsKey("foo") ? dict["foo"] : 0);
dict["foo"] = 42; // invalidates fooValue
Console.WriteLine(fooValue.Value); // 42
```

### Commands (`FunctionCommand`)
Create commands with automatic `CanExecute` tracking, optional async execution, and execution counting.
```csharp
var canExecute = Reactive.Ref(true);
var cmd = FunctionCommand.Create<object>(_ => DoWork(), canExecute: () => canExecute.Value);
cmd.CanExecuteChanged += (s, e) => { /* react to can-execute changes */ };

// Async variant with concurrency control
var asyncCmd = FunctionCommand.CreateFromTask<object>(async _ => {
    await Task.Delay(100);
    return true;
}, canExecute: () => canExecute.Value, allowConcurrentExecution: false);

// Observe executing count
var executingCount = asyncCmd.ExecutingCount.Value; // IReadOnlyRef<int>
```

## Usage Examples

See [a-littlebit/EffectSharp · GitHub](https://github.com/a-littlebit/EffectSharp/tree/main/Examples/) for more usage examples.

## Source Generators

EffectSharp provides a source generator project [EffectSharp.SourceGenerators](EffectSharp.SourceGenerators/README.md) that generates reactive models, computed properties, watchers, and function commands via attributes.

## Advanced Topics
### Notification Batching
`TaskManager` batches both effect triggers and UI notifications. Configure the `TaskBatcher` instances as needed:
```csharp
TaskManager.CreateEffectBatcherIfAbsent(() =>
{
    var batcher = new TaskBatcher<Effect>(
        batchProcessor: DefaultEffectBatchProcessor, // process effects
        intervalMs: 0, // execute once the target scheduler is idle
        scheduler: TaskScheduler.FromCurrentSynchronizationContext(), // e.g., UI thread
        maxConsumers: 1 // single-threaded processing
    );
    batcher.BatchProcessingFailed += TraceEffectFailure; // log failures
    return batcher;
});
TaskManager.CreateNotificationBatcherIfAbsent(() =>
{
    // TODO: create your custom notification batcher
});
```
Use `await Reactive.NextTick()` to await both effect and notify ticks.

### Custom Effect Schedulers
Provide a scheduler to delay or merge effect executions:
```csharp
Timer? timer = null;
var effect = Reactive.Effect(() => DoWork(), eff => {
    // simple debounce
    timer?.Dispose();
    timer = new Timer(_ => eff.Execute(), null, 50, Timeout.Infinite);
});
```

### List Diff & Binding
Bind a source list to an `ObservableCollection<T>` with diffing:
```csharp
var source = Reactive.Collection<Item>();
var target = Reactive.Collection<Item>();
target.BindTo(() => {
    return source.Where(item => item.IsActive)
        .OrderBy(item => item.Name)
        .ToList()
}, keySelector: item => item.Id);
```
Or directly create a computed list:
```csharp
var computedList = Reactive.ComputedList<Item, List<Item>, long>(() => {
    return source.Where(item => item.IsActive)
        .OrderBy(item => item.Name)
        .ToList();
}, keySelector: item => item.Id);
```
This minimizes updates to `target` based on the longest increasing subsequence (LIS) algorithm.

### Concurrency Guarantees

EffectSharp is designed to be **safe by default**.
Most applications will use the **default single-threaded scheduling** to drive the reactive system **(not the whole application)**, and do not need to worry about concurrency at all.

For advanced scenarios:

* **Reactive values** (`Ref<T>`, `ReactiveProxy<T>`) use *atomic writes* and always propagate changes with an eventual-consistency guarantee.
* **Effects** never run concurrently with themselves; each `Effect` uses an `AsyncLock` to protect dependency tracking. But try not to do any heavy work in an effect.
* **Computed values** use atomic invalidation and 3-state machine to allow being invalidated at any time without returning stale results.
* **Reactive collections** (`ReactiveCollection<T>`, `ReactiveDictionary<TKey,TValue>`) are *not thread-safe* and should be modified only from a single thread.
* When configuring custom or parallel schedulers via `TaskManager`, EffectSharp remains stable, but **strict update ordering is no longer guaranteed**. This mode is intended for advanced users who understand the trade-offs between throughput and deterministic reactivity.

In short:
**Default mode provides strong, predictable consistency.
Custom parallel execution provides flexibility with eventual consistency.**

## Comparison with Vue 3
| Vue 3 Concept | EffectSharp Equivalent |
|---------------|------------------------|
| `ref()`       | `Reactive.Ref()` |
| `reactive()`  | `Reactive.Create()` |
| `computed()`  | `Reactive.Computed()` |
| `watch()`     | `Reactive.Watch()` |
| `effect` (internal) | `Reactive.Effect()` |
| Track dependencies | `Dependency.Track()` / Property interception |
| `nextTick()` | `Reactive.NextTick()` |
| Microtask queue | `TaskManager` |

## Limitations
- Prefer interface-only design: define interfaces and use `Reactive.Create<IYourInterface>()` so EffectSharp provides the implementation.
If you need to use classes, ensure they implement an interface with properties you want to be reactive.
- Structs/records are not reactive directly (wrap them inside `Ref<T>`).
- Diff-binding is provided for lists to `ObservableCollection<T>`.
- Deep tracking may increase overhead on large graphs; configure per-property via `ReactivePropertyAttribute`.
- No persistence layer integration yet.

## FAQ
**Q: Do I need to adjust `TaskManager` Configuration?**  
Usually no, sensible defaults are provided. Adjust only if you need custom batching behavior,
or your application requires specific threading contexts (e.g., specific UI thread).

**Q: How do I stop tracking?**  
Dispose the `Effect` returned by `Reactive.Effect()` or `Reactive.Watch()`.  
If you want to perform side effects without tracking dependencies, use `Effect.Untracked(() => { ... })`.

**Q: How does Deep Watch work?**  
Deep watching tracks changes on the selected value and all reactive values inside it, by invoking the outmost object's `TrackDeep` method (from the IReactive interface).  
To use it safely, mark nested properties with `[ReactiveProperty(deep: true)]` so they become reactive objects — but avoid applying `deep: true` everywhere, as it increases proxy creation cost and may cause unnecessary recursion. Use it only when you truly need to watch an entire object graph.

**Q: What is the difference between keyed and unkeyed diff-binding?**  
Keyed binding is preferred when items have a unique identifier property, it is usually more efficient.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
