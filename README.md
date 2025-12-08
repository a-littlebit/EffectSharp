# EffectSharp

> Lightweight reactive state management for .NET with a Vue 3 inspired API (`ref`, `computed`, `watch`, `effect`, reactive objects, collections & dictionaries, list diff-binding).

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
    - [`Ref<T>`]
    - Reactive Objects (`Reactive.Create` / `CreateDeep` / `TryCreate`)
    - Computed Values (`Reactive.Computed`)
    - Effects (`Reactive.Effect`)
    - Watching Changes (`Reactive.Watch`)
    - Reactive Collections (`Reactive.Collection`)
    - Reactive Dictionaries (`Reactive.Dictionary`)
- [Usage Examples](#usage-examples)
- [Advanced Topics](#advanced-topics)
    - Notification Batching & `TaskManager` configuration
    - Custom Effect Schedulers
    - Deep Watching & `NonReactive` opt-outs
    - List Diff & Binding (`DiffAndBindTo`)
- [Comparison with Vue 3](#comparison-with-vue-3)
- [Limitations](#limitations)
- [FAQ](#faq)
- [License](#license)

## Overview
EffectSharp brings a fine-grained reactive system to .NET similar to Vue 3's reactivity core. Instead of heavy global state containers, you compose reactive primitives that automatically track dependencies and propagate updates efficiently.

Recent updates include:
- Deep mode across refs, computed values, proxies, and collections/dictionaries.
- Reactive dictionaries with per-key dependency tracking.
- Diff-based binding from lists to `ObservableCollection<T>`.
- Unified async batching with `TaskManager` plus `Reactive.NextTick()`.

It focuses on:
- Minimal Ceremony: Opt-in reactivity with clear primitives.
- Predictable Updates: Dependency tracking via transparent property access.
- Performance: Computed values are lazily evaluated and cached until invalidated.
- UI Friendly: Batched `INotifyPropertyChanged` events reduce UI churn (e.g., WPF, MAUI).

## Key Features
- `Ref<T>` primitive for single mutable reactive values, optional deep mode.
- Reactive proxies for plain class instances (virtual properties required), deep wrapping via `SetDeep()`.
- `TryCreate` helper to wrap if possible, otherwise return original.
- Dependency-tracked computed values (`Computed<T>`) with lazy evaluation & caching (supports deep mode).
- Effects that auto re-run when dependencies change; optional scheduling; `Untracked` helpers.
- Flexible watchers: `Ref<T>`, computed getter functions, tuples and complex shapes, deep tracking.
- Reactive `ObservableCollection<T>` enhancement with per-index and list-level dependencies.
- Reactive dictionaries with per-key tracking and key-set dependency.
- LIS-based diff & binding: bind source lists to `ObservableCollection<T>` with minimal updates.
- Unified batching via `TaskManager`; `Reactive.NextTick()` awaits both effect and notify cycles.

## Quick Start

### Create a new reactive object, ref, computed value, effect, and watch changes:
```csharp
using EffectSharp;

// Reactive object (class must be non-sealed; virtual properties needed for proxy)
var product = Reactive.Create(new Product { Name = "Laptop", Price = 1000 });

// Ref primitive (with optional deep mode)
var count = Reactive.Ref(0);
var orderRef = Reactive.Ref(new Order { Product = new Product { Name = "Phone", Price = 500 }, Quantity = 1 }, deep: true);

// Computed value
var priceWithTax = Reactive.Computed(() => product.Price + (int)(product.Price * 0.1));

// Effect (auto reruns when dependencies used inside change)
var effect = Reactive.Effect(() => {
    Console.WriteLine($"Total: {priceWithTax.Value}");
});

product.Price = 1200; // Effect will re-run

// Watch a ref (returns an Effect; dispose to stop)
var subEffect = Reactive.Watch(count, (newVal, oldVal) => {
    Console.WriteLine($"count changed {oldVal} -> {newVal}");
});
count.Value = 1;
await Reactive.NextTick();
subEffect.Dispose();

// Reactive collection
var numbers = Reactive.Collection<int>();
var sum = Reactive.Computed(() => numbers.Sum());
numbers.Add(5); // sum invalidated
Console.WriteLine(sum.Value); // 5
```

### Bind to UI (WPF example)
```csharp
using CommunityToolkit.Mvvm.Input;
using EffectSharp;

// ViewModel with reactive properties
public class MyViewModel
{
    public Ref<int> Counter { get; } = Reactive.Ref(0);
    public Computed<string> DisplayText { get; }

    public MyViewModel()
    {
        DisplayText = Reactive.Computed(() => $"Counter: {Counter.Value}");
    }
    
    [RelayCommand]
    public void Increment() => Counter.Value++;

    [RelayCommand]
    public void Decrement() => Counter.Value--;
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
Reactive.Effect(() => Console.WriteLine(counter.Value));
counter.Value++; // Effect prints updated value
```

### Reactive Objects (`Reactive.Create` / `CreateDeep` / `TryCreate`)
Wrap an existing instance in a dynamic proxy using Castle.DynamicProxy. Requirements:
- Class must be non-sealed.
- Properties you want tracked must be `virtual`.
`CreateDeep` additionally enables deep behavior via `SetDeep()` internally. `TryCreate` returns the original instance if it cannot be proxied.
```csharp
var order = Reactive.CreateDeep(new Order {
    Product = new Product { Name = "Phone", Price = 500 },
    Quantity = 2
});
Reactive.Effect(() => Console.WriteLine(order.Product.Price));
order.Product.Price = 600; // Effect reruns
```

### Computed Values (`Reactive.Computed`)
Lazy, cached derivations. Recomputed only when one of the dependencies accessed during its last evaluation changes.
```csharp
var product = Reactive.Create(new Product { Name = "Tablet", Price = 300 });
var priceWithTax = Reactive.Computed(() => product.Price + (int)(product.Price * 0.1));
Console.WriteLine(priceWithTax.Value); // 330
product.Price = 400;
Console.WriteLine(priceWithTax.Value); // 440 (recomputed)
```

### Effects (`Reactive.Effect`)
Encapsulate reactive logic. Any dependency read during its execution registers it as a subscriber. Supports custom scheduler for throttling/coalescing and `Untracked` helpers to perform side effects without capturing dependencies.
```csharp
var throttled = Reactive.Effect(() => {
    _ = product.Price; // Track dependency
}, effect => {
    // Simple scheduler example: defer with Task.Run
    Task.Run(() => effect.Execute());
});
```

### Watching Changes (`Reactive.Watch`)
`Watch` lets you observe specific sources without executing a full effect body manually. It returns an `Effect` you can dispose to stop watching. Variants:
- Ref-based: `Watch(refObj, (newVal, oldVal) => ...)`
- Getter-based: `Watch(() => (refA.Value, refB.Value), callback)`; supports tuples and enumerable shapes
- Options: `Immediate`, `Deep`, `EqualityComparer<T>`
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
var hasFoo = Reactive.Computed(() => dict.ContainsKey("foo"));
Console.WriteLine(hasFoo.Value); // false
dict["foo"] = 2; // invalidates hasFoo via key-set dependency
await Reactive.NextTick();
Console.WriteLine(hasFoo.Value); // true
```

## Usage Examples

See [a-littlebit/EffectSharp · GitHub](https://github.com/a-littlebit/EffectSharp/tree/main/Examples/) for more usage examples.

## Advanced Topics
### Notification Batching
`TaskManager` batches both effect triggers and UI notifications. Configure intervals/schedulers as needed:
```csharp
TaskManager.EffectIntervalMs = 0; // process effects ASAP
TaskManager.NotifyIntervalMs = 16; // UI-friendly batching
TaskManager.FlushNotifyAfterEffectBatch = true; // auto flush after effect batch
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

### Deep Watching
`WatchOptions.Deep = true` tracks nested reactive properties (including refs/collections/dictionaries). Use the `NonReactive` attribute to opt out for specific properties/classes.

### List Diff & Binding
Bind a source list to an `ObservableCollection<T>` with diffing:
```csharp
var source = Reactive.Collection<Item>();
var target = Reactive.Collection<Item>();
Reactive.Computed(() => {
    source.Where(item => item.IsActive)
        .OrderBy(item => item.Name)
        .ToList()
}).DiffAndBindTo(target, keySelector: item => item.Id);
```
This minimizes updates to `target` based on the longest increasing subsequence (LIS) algorithm.

## Comparison with Vue 3
| Vue 3 Concept | EffectSharp Equivalent |
|---------------|------------------------|
| `ref()`       | `Reactive.Ref()` |
| `reactive()`  | `Reactive.Create()` / `Reactive.CreateDeep()` |
| `computed()`  | `Reactive.Computed()` |
| `watch()`     | `Reactive.Watch()` |
| `effect` (internal) | `Reactive.Effect()` |
| Track dependencies | `Dependency.Track()` / Property interception |
| `nextTick()` | `Reactive.NextTick()` |
| Microtask queue | `TaskManager` configuration |

## Limitations
- Only non-sealed classes with `virtual` properties are proxied.
- Structs / records (non-class) are not reactive directly (wrap them inside a `Ref<T>`).
- Diff-binding provided for lists to `ObservableCollection<T>`.
- Deep watch may cause overhead on large graphs.
- No persistence layer integration yet.
- Use `NonReactive` to exclude properties/classes from tracking.

## FAQ
**Q: Do I need to adjust `TaskManager` Configuration?**  
Usually no, sensible defaults are provided. Adjust only if you need custom batching behavior,
or your application requires specific threading contexts (e.g., specific UI thread).

**Q: How do I stop tracking?**  
Dispose the `Effect` returned by `Reactive.Effect()` or `Reactive.Watch()`.  
If you want to perform side effects without tracking dependencies, use `Effect.Untracked(() => { ... })`.

**Q: Is EffectSharp thread-safe?**  
Reactive proxies are thread-safe or not depending on the underlying object except for deep ones,
which are not thread-safe until `SetDeep()` is done.  
`Ref<T>` is not thread-safe. Use `AtomicObjectRef<T>`, `AtomicIntRef`, etc. for thread-safe refs.
Effects and watchers are scheduled via `TaskManager` and each runs with a lock to prevent concurrent execution,
which means effects/watchers themselves are thread-safe.  
Note that the UI notification scheduler in `TaskManager` should be set to the UI thread context when used in UI applications.
The default is `TaskScheduler.FromCurrentSynchronizationContext()`.

**Q: What is the difference between keyed and unkeyed diff-binding?**  
Keyed binding is preferred when items have a unique identifier property, it is usually more efficient.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.