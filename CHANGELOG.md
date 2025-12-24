# Changelog

All notable changes to this project will be documented in this file.

This project adheres to Keep a Changelog and uses Semantic Versioning (when versions are published). For now, changes are grouped by version and date.

## Unreleased

### Added

- Source generator now supports specifying custom fields/properties to be included in deep tracking via `[Deep]` attribute.
- Added constructor overloads to source generator attributes for more convenient usage.

### Fixed

- `ReactiveDictionary<TKey,TValue>`: fixed `Count` property not tracking dependencies properly.
- Fixed potential filename conflict when mark [ReactiveModel] on multiple classes with the same name but different count of generic parameters.

### Enhancement

- Improved diff algorithm performance scenario when few changes occur in large lists.

## 1.3.1 - 2025-12-22

### Added

- Added nullable annotations to public APIs.

### Fixed
- Source generator not working when referenced via nuget.

## 1.3.0 - 2025-12-21

### Added

- Source generator support! [EffectSharp.SourceGenerators](https://github.com/a-littlebit/EffectSharp/tree/main/EffectSharp.SourceGenerators) provides compile-time generation of reactive models, computed properties, watchers, and function commands via attributes. See the [README](EffectSharp.SourceGenerators/README.md) for details.
- `TaskBatcher` now supports custom throttlers to control task scheduling rates.
- `Reactive.NextTick(...)` could now specify a cancellation token to cancel waiting.
- `Reactive.ComputedList(...)`: directly create a `ReactiveCollection<T>` with diffing support from a method returning `IList<T>`.
- `Reactive.Watch(...)` now supports `once` option to auto-unsubscribe after first invocation.

### Changed

- `TaskManager` now provides `CreateEffectBatcherIfAbsent(...)` and `CreateNotificationBatcherIfAbsent(...)` methods for more flexible batcher initialization, instead of static properties.
- Renamed `Reactive.DiffAndBindTo` to `Reactive.BindTo` and changed parameter order and result type (to `Effect`) for better usability.
- `Reactive.Watch(...)` now supports named parameters for options instead of a separate `WatchOptions<T>` class.
- `TaskBatcher.NextTick(...)` and `Reactive.NextTick(...)` now does not propagate exceptions thrown in scheduled actions; exceptions could be observed via related events.

### Fixed

- `TaskBatcher.FlushAsync(...)` may fail to cancel a throttling delay if it hasn't started yet.
- `ReactiveCollection<T>.Contains(...)` dose not track dependencies properly when called inside an effect.
- `ReactiveProxy<T>.InitializeForTarget(...)` missing null check for target parameter and may throw unexpected exceptions.

### Removed

- `WatchOptions<T>` class removed; use named parameters on `Reactive.Watch` instead.

### Enhancement

- The default `TaskManager.NotificationBatcher` now uses a new throttling strategy that waits for the current effect batch to complete before processing notifications, reducing redundant effect executions during high-frequency updates.
- `Reactive.BindTo(...)` disables equality comparison in `Reactive.Watch` - the diffing algorithm will handle it.
- `Effect` allows directly calling locked methods during effect execution without an `AsyncLock.Scope`.

## 1.2.2 - 2025-12-14

### Changed

- `TaskManager` now creates default `TaskBatcher`s on `TaskScheduler.Default` if `SynchronizationContext.Current` is null, improving compatibility in non-UI contexts.
- `TaskBatcher` now dequeue tasks on the specified `TaskScheduler` to increase merging granularity when the specified scheduler is busy.
- `Reactive.NextTick(...)` now supports cancellation tokens.

## 1.2.1 - 2025-12-12

### Added
- `AsyncLock`: a lightweight async-compatible lock for critical sections, with scopes for reentrancy.
- `ReactivePropertyAttribute` now supports `EqualityComparer` parameter for custom equality checks on property sets.

### Changed
- `TaskBatcher` now supports specifying maximum consumer count to control concurrency levels.
- `TaskManager` updated to allow custom `TaskBatcher` initialization for more flexible task scheduling strategies.
- `Effect` switched to use `AsyncLock` for improved concurrency handling.

### Fixed
- `AtomicDouble`: fixed wrong initial value assignment issue.

## 1.2.0 - 2025-12-10

This release focuses on four main areas: LIS-based diff, command API, interface-based proxying, and atomic reactive references, plus targeted bug fixes.

### Added
- LIS-based list diff algorithm: `DiffAndBindTo` now uses a Longest Increasing Subsequence optimized strategy to minimize moves when syncing to `ObservableCollection<T>` (better performance for insert/remove/reorder heavy scenarios).
- FunctionCommand: new command abstraction for reactive actions (sync/async), designed to integrate with `Computed`/`Watch` and UI command bindings. Supports `CanExecute` and cancellation-friendly async.
- Interface-based reactive proxy: switch to interface-first proxy generation for better performance; attributes available to control proxy behavior.
- Atomic reactive references:
  - `AtomicIntRef` and `AtomicLongRef` provide thread-safe atomic operations (Increment/Decrement/Add/CompareExchange, etc.).

### Changed
- Reactive proxy generation moved to an interface-based approach using `System.Reflection` dynamic proxying. Attributes updated to reflect new proxy model and opt-ins/outs.
- `DiffAndBindTo` internal strategy updated to LIS-based optimization; external API remains the same.

### Fixed
- TaskBatcher: fixed a rare deadlock/spin scenario under concurrency that could lead to an infinite processing loop.
- Miscellaneous stability fixes in batching and effect scheduling.

### Notes
- Ref/Computed are now thread-safe; use atomic refs for shared counters.
- When using interface-based proxies, thread safety of specified instances depends on their own implementation (if target not specified, the default proxy will be thread-safe).

## 2025-12-03 and earlier

### Added
- `Reactive.Create(...)` and `Reactive.CreateDeep(...)` for reactive object proxies.
- `Reactive.Ref(value)` for reactive references.
- `Reactive.Effect(action[, scheduler][, lazy])` with optional scheduler and untracked helpers.
- `Reactive.Computed(getter[, setter])` lazy cached derivations.
- `Reactive.Collection<T>` with dependency tracking for indexed access and the whole collection.
- `Reactive.Dict<K,V>` with dependency tracking for per-key access and the whole key set.
- `Reactive.Watch(...)` variants for refs and getters with `WatchOptions` (Immediate, Deep).
- `Reactive.DiffAndBindTo(...)` for syncing `IRef<IList<T>>` to `ObservableCollection<T>` using two-pointer diff algorithm.
- `TaskManager` and `TaskBatcher` for efficient task scheduling and batching.

### Fixed
- Multiple correctness fixes in dependency subscription lifecycle and grouping notifications.

---
When publishing a versioned release, replace the Unreleased section with a proper version (e.g., `## 1.0.0 - 2025-12-10`) and summarize changes accordingly.