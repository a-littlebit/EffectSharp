using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace EffectSharp
{
    /// <summary>
    /// Provides static methods for managing property-based dependencies and tracking changes for reactive effect
    /// systems.
    /// </summary>
    public static class DependencyManager
    {
        private static readonly ConditionalWeakTable<object, DependencyObject> _dependencies
            = new ConditionalWeakTable<object, DependencyObject>();

        private static readonly ConditionalWeakTable<Type, PropertyInfo[]> _reactivePropertyCache
            = new ConditionalWeakTable<Type, PropertyInfo[]>();

        public static Dependency GetDependency(object owner, string propertyName)
        {
            var depObj = _dependencies.GetOrCreateValue(owner);
            return depObj.GetDependency(propertyName);
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

        public static bool SetDeep(object owner)
        {
            var depObj = _dependencies.GetOrCreateValue(owner);
            return depObj.SetDeep();
        }

        public static bool IsDeep(object owner)
        {
            DependencyObject depObj;
            if (_dependencies.TryGetValue(owner, out depObj))
            {
                return depObj.IsDeep;
            }
            return false;
        }

        public static PropertyInfo[] GetReactiveProperties(Type type)
        {
            return _reactivePropertyCache.GetValue(type, ReactiveInterceptor.GetReactivePropertiesInternal);
        }
    }

    internal class DependencyObject
    {
        internal ConcurrentDictionary<string, Dependency> Dependencies { get; }
            = new ConcurrentDictionary<string, Dependency>();

        private bool _isDeep = false;

        internal Dependency GetDependency(string propertyName)
        {
            return Dependencies.GetOrAdd(propertyName, _ => new Dependency());
        }

        internal bool IsDeep => _isDeep;

        internal bool SetDeep()
        {
            if (_isDeep) return false;
            _isDeep = true;
            return true;
        }
    }
}
