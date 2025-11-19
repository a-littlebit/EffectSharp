using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp;

/// <summary>
/// Core implementation for tracking dependencies between reactive properties and their effects.
/// </summary>
public static class DependencyTracker
{
    private static readonly ConditionalWeakTable<object, Dictionary<string, Dependency>> _dependencies
        = new();

    private static readonly AsyncLocal<Stack<List<Dependency>>?> _currentTrackingDependencies
        = new();

    private static readonly TaskBatcher<NotifyTask> _notifyBatcher
        = new(NotifyBatch, 16);

    public static TaskScheduler NotifyTaskScheduler
    {
        get => _notifyBatcher.Scheduler;
        set => _notifyBatcher.Scheduler = value;
    }

    public static void EnqueueNotify(object model, string propertyName, Action<PropertyChangedEventArgs> notifier)
    {
        var task = new NotifyTask
        {
            Model = model,
            PropertyName = propertyName,
            Notifier = notifier
        };
        _notifyBatcher.Enqueue(task);
    }

    public static void FlushNotifyQueue()
    {
        _notifyBatcher.Flush();
    }

    private static void NotifyBatch(IEnumerable<NotifyTask> tasks)
    {
        var grouped = tasks.GroupBy(t => (t.Model, t.PropertyName));
        foreach (var group in grouped)
        {
            var (_, propName) = group.Key;
            group.First().Notifier(new PropertyChangedEventArgs(propName));
        }
    }

    public static Dependency GetDependency(object owner, string propertyName)
    {
        if (!_dependencies.TryGetValue(owner, out var propertyMap))
        {
            propertyMap = new Dictionary<string, Dependency>();
            _dependencies.Add(owner, propertyMap);
        }

        if (!propertyMap.TryGetValue(propertyName, out var dependency))
        {
            dependency = new Dependency();
            propertyMap[propertyName] = dependency;
        }

        return dependency;
    }

    public static void TrackDependency(object owner, string propertyName)
    {
        if (Effect.CurrentEffect is null) return;
        GetDependency(owner, propertyName).Track();
    }

    public static void TriggerDependency(object owner, string propertyName)
    {
        GetDependency(owner, propertyName).Trigger();
    }

    internal static void StartDependencyTracking()
    {
        _currentTrackingDependencies.Value ??= new();
        _currentTrackingDependencies.Value.Push(new());
    }

    internal static void DependencyTracked(Dependency dependency)
    {
        if (_currentTrackingDependencies.Value is null || _currentTrackingDependencies.Value.Count == 0)
            return;
        _currentTrackingDependencies.Value.Peek().Add(dependency);
    }

    internal static List<Dependency>? RetrieveAndClearTrackedDependencies()
    {
        if (_currentTrackingDependencies.Value is null || _currentTrackingDependencies.Value.Count == 0)
            throw new InvalidOperationException("No active dependency tracking session.");
        var tracked = _currentTrackingDependencies.Value.Pop();
        if (_currentTrackingDependencies.Value.Count == 0)
            _currentTrackingDependencies.Value = null;
        return tracked;
    }
}

internal class NotifyTask
{
    internal required object Model { get; set; }
    internal required string PropertyName { get; set; }
    internal required Action<PropertyChangedEventArgs> Notifier { get; set; }
}
