using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp
{

    /// <summary>
    /// Reactive Interceptor: Intercepts property access to track dependencies and notify changes.
    /// </summary>
    internal class ReactiveInterceptor : IInterceptor
    {
        public event PropertyChangingEventHandler PropertyChanging;
        public event PropertyChangedEventHandler PropertyChanged;

        public void Intercept(IInvocation invocation)
        {
            var target = invocation.Proxy;
            var targetMethod = invocation.Method;
            var methodName = targetMethod.Name;

            object Deep(object value)
            {
                if (value == null) return null;
                if (value is IReactive r)
                {
                    r.SetDeep();
                    return value;
                }
                else
                {
                    var reactiveValue = Reactive.TryCreate(value);
                    if (reactiveValue is IReactive rNew)
                    {
                        rNew.SetDeep();
                        return reactiveValue;
                    }
                    return value;
                }
            }

            if (!targetMethod.IsSpecialName)
            {
                if (targetMethod.DeclaringType == typeof(IReactive) && targetMethod.Name == nameof(IReactive.SetDeep))
                {
                    if (!DependencyManager.SetDeep(target))
                    {
                        invocation.ReturnValue = false;
                        return;
                    }

                    invocation.ReturnValue = true;
                    foreach (var property in DependencyManager.GetReactiveProperties(target.GetType()))
                    {
                        var value = property.GetValue(target);
                        var deepValue = Deep(value);
                        if (!ReferenceEquals(value, deepValue))
                        {
                            property.SetValue(target, deepValue);
                        }
                    }
                }
                else if (targetMethod.DeclaringType == typeof(IReactive) && targetMethod.Name == nameof(IReactive.TrackDeep))
                {
                    foreach (var property in DependencyManager.GetReactiveProperties(target.GetType()))
                    {
                        DependencyManager.TrackDependency(target, property.Name);
                        var value = property.GetValue(target);
                        if (value == null) return;
                        if (value is IReactive r)
                        {
                            r.TrackDeep();
                        }
                    }
                }
                else
                {
                    invocation.Proceed();
                }
                return;
            }

            if (methodName.StartsWith("set_"))
            {
                var propertyName = methodName.Substring(4);
                var propertyInfo = target.GetType().GetProperty(propertyName);
                PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(propertyName));
                if (DependencyManager.IsDeep(target))
                {
                    var value = invocation.Arguments[0];
                    var deepValue = Deep(value);
                    if (!ReferenceEquals(value, deepValue))
                    {
                        invocation.SetArgumentValue(0, deepValue);
                    }
                }
                invocation.Proceed();
                if (PropertyChanged != null)
                {
                    TaskManager.EnqueueNotify(this, propertyName, (e) =>
                    {
                        PropertyChanged?.Invoke(this, e);
                    });
                }
                DependencyManager.TriggerDependency(target, propertyName);
            }
            else if (methodName.StartsWith("get_"))
            {
                var propertyName = methodName.Substring(4);
                DependencyManager.TrackDependency(target, propertyName);
                invocation.Proceed();
            }
            else if (targetMethod.Name == "add_PropertyChanging")
            {
                var handler = (PropertyChangingEventHandler)invocation.Arguments[0];
                PropertyChanging += handler;
            }
            else if (targetMethod.Name == "remove_PropertyChanging")
            {
                var handler = (PropertyChangingEventHandler)invocation.Arguments[0];
                PropertyChanging -= handler;
            }
            else if (targetMethod.Name == "add_PropertyChanged")
            {
                var handler = (PropertyChangedEventHandler)invocation.Arguments[0];
                PropertyChanged += handler;
            }
            else if (targetMethod.Name == "remove_PropertyChanged")
            {
                var handler = (PropertyChangedEventHandler)invocation.Arguments[0];
                PropertyChanged -= handler;
            }
            else
            {
                invocation.Proceed();
            }
        }

        public static bool CanProxyType(Type type)
        {
            return type.IsClass &&
                   !type.IsSealed &&
                   !typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
                   type.GetCustomAttribute(typeof(NonReactive)) == null;
        }

        public static bool CanProxyProperty(PropertyInfo property)
        {
            return property.GetCustomAttribute(typeof(NonReactive)) == null &&
                   property.CanRead && property.CanWrite &&
                   property.GetMethod.IsVirtual && !property.GetMethod.IsFinal &&
                   property.SetMethod.IsVirtual && !property.SetMethod.IsFinal;
        }

        internal static PropertyInfo[] GetReactivePropertiesInternal(Type type)
        {
            if (!CanProxyType(type))
            {
                return Array.Empty<PropertyInfo>();
            }

            var dependencyProperties = new List<PropertyInfo>();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (CanProxyProperty(property))
                {
                    dependencyProperties.Add(property);
                }
            }
            return dependencyProperties.ToArray();
        }
    }
}
