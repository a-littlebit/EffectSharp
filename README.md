# EffectSharp

> Lightweight reactive state management for .NET with a Vue 3 inspired API (`ref`, `computed`, `watch`, `effect`, reactive objects & collections).

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
  - [`Ref<T>`]
  - Reactive Objects (`Reactive.Create` / `CreateDeep`)
  - Computed Values (`Reactive.Computed`)
  - Effects (`Reactive.Effect`)
  - Watching Changes (`Reactive.Watch`)
  - Reactive Collections (`Reactive.Collection`/`Reactive.Dictionary`)
- [Usage Examples](#usage-examples)
- [Advanced Topics](#advanced-topics)
  - Notification Batching & `DependencyTracker.FlushNotifyQueue()`
  - Custom Effect Schedulers
  - Deep Watching
- [Comparison with Vue 3](#comparison-with-vue-3)
- [Limitations](#limitations)
- [FAQ](#faq)

## Overview
EffectSharp brings a fine-grained reactive system to .NET similar to Vue 3's reactivity core. Instead of heavy global state containers, you compose reactive primitives that automatically track dependencies and propagate updates efficiently.

It focuses on:
- Minimal Ceremony: Opt-in reactivity with clear primitives.
- Predictable Updates: Dependency tracking via transparent property access.
- Performance: Computed values are lazily evaluated and cached until invalidated.
- UI Friendly: Batched `INotifyPropertyChanged` events reduce UI churn (e.g., WPF, MAUI).

## Key Features
- `Ref<T>` primitive for single mutable reactive values.
- Reactive proxies for plain class instances (virtual properties required) and deep wrapping of nested references.
- Dependency-tracked computed values (`Computed<T>`) with lazy evaluation & caching.
- Effects that auto re-run when dependencies change; optional scheduling.
- Flexible watchers: ref values, object properties, computed getter functions, deep trees.
- Reactive observable collection with dependable `Count` and item tracking.
- Reactive dictionary that tracks dependencies on individual keys and the overall key set.
- Batched property change notifications (flush manually when required).
- Simple, test-first API (mirrors concepts in Vue 3).

## Quick Start

### Create a new reactive object, ref, computed value, effect, and watch changes:
```csharp
using EffectSharp;

// Reactive object (class must be non-sealed; virtual properties needed for proxy)
var product = Reactive.Create(new Product { Name = "Laptop", Price = 1000 });

// Ref primitive
var count = Reactive.Ref(0);

// Computed value
var priceWithTax = Reactive.Computed(() => product.Price + (int)(product.Price * 0.1));

// Effect (auto reruns when dependencies used inside change)
var effect = Reactive.Effect(() => {
    Console.WriteLine($"Total: {priceWithTax.Value}");
});

product.Price = 1200; // Effect will re-run

// Watch a ref
var sub = Reactive.Watch(count, (newVal, oldVal) => {
    Console.WriteLine($"count changed {oldVal} -> {newVal}");
});
count.Value = 1;

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

### Reactive Objects (`Reactive.Create` / `CreateDeep`)
Wrap an existing instance in a dynamic proxy using Castle.DynamicProxy. Requirements:
- Class must be non-sealed.
- Properties you want tracked must be `virtual`.
`CreateDeep` additionally wraps nested object properties.
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
Encapsulate reactive logic. Any dependency read during its execution registers it as a subscriber. Supports custom scheduler for throttling / coalescing.
```csharp
var throttled = Reactive.Effect(() => {
    _ = product.Price; // Track dependency
}, effect => {
    // Simple scheduler example: defer with Task.Run
    Task.Run(() => effect.Execute());
});
```

### Watching Changes (`Reactive.Watch`)
`Watch` lets you observe specific sources without executing a full effect body for dependency collection manually.
Variants:
- Property-based: `Watch(reactiveObject, "Price", callback)`
- Ref-based: `Watch(refObj, (newVal, oldVal) => ...)`
- Getter-based: `Watch(() => (refA.Value, refB.Value), callback)`
- Options: `Immediate`, `Deep`
```csharp
var refA = Reactive.Ref(1);
var refB = Reactive.Ref(10);
var sub = Reactive.Watch(() => (refA.Value, refB.Value), () => Console.WriteLine("Either changed"));
refA.Value = 2; // triggers
refB.Value = 20; // triggers
```

### Reactive Collections (`Reactive.Collection<T>`/`Reactive.Dictionary<TKey, TValue>`)
`ReactiveCollection<T>` extends `ObservableCollection<T>` with tracked dependencies for:

- `Count`
- Indexed access (`Item[]`)
```csharp
var list = Reactive.Collection([10, 20, 30]);
var first = Reactive.Computed(() => list[0]);
list[0] = 100; // invalidates first
Console.WriteLine(first.Value); // 100
```

`ReactiveDictionary<TKey, TValue>` implements `IDictionary<TKey, TValue>` with tracked dependencies for:

- Individual keys
- Overall key set

```csharp
var dict = Reactive.Dictionary<string, string>();
var hasFoo = Reactive.Computed(() => reactiveDict.ContainsKey("foo"));
Console.WriteLine(hasFoo.value); // false
dict["foo"] = "bar"; // invalidates hasFoo
Console.WriteLine(hasFoo.value); // true
```

## Usage Examples

### Computed Chaining
```csharp
var discounted = Reactive.Computed(() => product.Price - 20);
var final = Reactive.Computed(() => discounted.Value + (int)(discounted.Value * 0.1));
```
### Watching Deep Nested Structures
```csharp
var orderRef = Reactive.Ref(new Order {
    Product = new Product { Name = "Widget", Price = 100 },
    Quantity = 1
});
var sub = Reactive.Watch(orderRef, () => {
    _ = orderRef.Value.Product.Price; // track nested
}, new WatchOptions { Deep = true });
orderRef.Value.Product.Price = 150; // triggers
```
### Manual Flush (if needed for UI testing)
`PropertyChanged` notifications are batched (default 16ms). In test code or tight loops:

```csharp
DependencyTracker.FlushNotifyQueue();
```

## Advanced Topics
### Notification Batching
`TaskBatcher` collects property change notifications and raises them in grouped batches to reduce UI overhead. Use `DependencyTracker.FlushNotifyQueue()` to force synchronous dispatch (e.g., unit tests).

### Custom Effect Schedulers
Provide a scheduler to delay or merge effect executions:
```csharp
var effect = Reactive.Effect(() => DoWork(), eff => {
    // simple debounce
    Timer? timer = null;
    timer?.Dispose();
    timer = new Timer(_ => eff.Execute(), null, 50, Timeout.Infinite);
});
```

### Deep Watching
`WatchOptions.Deep = true` traverses nested reactive properties and subscribes to all dependencies. Use carefully for large graphs.

## Comparison with Vue 3
| Vue 3 Concept | EffectSharp Equivalent |
|---------------|------------------------|
| `ref()`       | `Reactive.Ref()` |
| `reactive()`  | `Reactive.Create()` / `Reactive.CreateDeep()` |
| `computed()`  | `Reactive.Computed()` |
| `watch()`     | `Reactive.Watch()` |
| `effect` (internal) | `Reactive.Effect()` |
| Track dependencies | Property interception / `DependencyTracker` |
| Flush microtasks | `DependencyTracker.FlushNotifyQueue()` |

## Limitations
- Only non-sealed classes with `virtual` properties are proxied.
- Structs / records (non-class) are not reactive directly (wrap them inside a `Ref<T>`).
- No built-in diffing; watchers fire on value inequality or explicit triggers.
- Deep watch may cause overhead on large graphs.
- No persistence layer integration yet.

## FAQ
**Q: Do I need `DependencyTracker.FlushNotifyQueue()` in production?**  
Usually no; batching improves performance. Use it in tests or deterministic update scenarios.

**Q: How do I stop tracking?**  
Dispose the `Effect` or unsubscribe the watcher (`IDisposable`).

**Q: Can I make a computed writable?**  
Pass a setter action: `Reactive.Computed(getter, setter)`; writing calls the setter and may mutate underlying refs.

**Q: How does caching work?**  
A computed recomputes only when invalidated by dependency triggers since its last evaluation.
