using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace EffectSharp
{
    /// <summary>
    /// Provides static methods for managing property-based dependencies and tracking changes for reactive effect
    /// systems.
    /// </summary>
    public static class DependencyManager
    {
        private static readonly ConditionalWeakTable<object, Dictionary<string, Dependency>> _dependencies
            = new ConditionalWeakTable<object, Dictionary<string, Dependency>>();

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
    }
}
