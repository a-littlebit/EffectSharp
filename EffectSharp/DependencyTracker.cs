using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Core implementation for tracking dependencies between reactive properties and their effects.
    /// </summary>
    public static class DependencyTracker
    {
        private static readonly ConditionalWeakTable<object, Dictionary<string, Dependency>> _dependencies
            = new ConditionalWeakTable<object, Dictionary<string, Dependency>>();

        private static readonly AsyncLocal<Stack<List<Dependency>>> _currentTrackingDependencies
            = new AsyncLocal<Stack<List<Dependency>>>();

        private static readonly TaskBatcher<NotifyTask> _notifyBatcher
            = new TaskBatcher<NotifyTask>((tasks) => NotifyBatch(tasks), 16);

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
            var propertyMap = _dependencies.GetOrCreateValue(owner);

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
            if (_currentTrackingDependencies.Value is null)
                _currentTrackingDependencies.Value = new Stack<List<Dependency>>();
            _currentTrackingDependencies.Value.Push(new List<Dependency>());
        }

        internal static void DependencyTracked(Dependency dependency)
        {
            if (_currentTrackingDependencies.Value is null || _currentTrackingDependencies.Value.Count == 0)
                return;
            _currentTrackingDependencies.Value.Peek().Add(dependency);
        }

        internal static List<Dependency> RetrieveAndClearTrackedDependencies()
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
        internal object Model { get; set; }
        internal string PropertyName { get; set; }
        internal Action<PropertyChangedEventArgs> Notifier { get; set; }
    }
}
