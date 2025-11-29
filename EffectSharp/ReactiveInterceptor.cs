using Castle.Core.Internal;
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

        private static object Deep(object value)
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

        private static bool SetDeep(object target)
        {
            if (!DependencyManager.SetDeep(target))
            {
                return false;
            }

            foreach (var property in DependencyManager.GetReactiveProperties(target.GetType()))
            {
                var value = property.GetValue(target);
                var deepValue = Deep(value);
                if (!ReferenceEquals(value, deepValue))
                {
                    property.SetValue(target, deepValue);
                }
            }
            return true;
        }

        private static void TrackDeep(object target)
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

        private void SetProperty(object target, string propertyName, IInvocation invocation)
        {
            var propertyInfo = target.GetType().GetProperty(propertyName);
            if (propertyInfo.GetAttribute<NonReactive>() != null)
            {
                invocation.Proceed();
                return;
            }
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

        private static void GetProperty(object target, string propertyName, IInvocation invocation)
        {
            var propertyInfo = target.GetType().GetProperty(propertyName);
            if (propertyInfo.GetAttribute<NonReactive>() == null)
            {
                DependencyManager.TrackDependency(target, propertyName);
            }
            invocation.Proceed();
        }

        public void Intercept(IInvocation invocation)
        {
            var target = invocation.Proxy;
            var targetMethod = invocation.Method;
            var methodName = targetMethod.Name;

            if (!targetMethod.IsSpecialName)
            {
                if (targetMethod.DeclaringType != typeof(IReactive))
                {
                    invocation.Proceed();
                }
                else if (targetMethod.Name == nameof(IReactive.SetDeep))
                {
                    invocation.ReturnValue = SetDeep(target);
                }
                else if (targetMethod.Name == nameof(IReactive.TrackDeep))
                {
                    TrackDeep(target);
                }
                else
                {
                    throw new NotImplementedException($"Method {targetMethod.Name} is not implemented in ReactiveInterceptor.");
                }
                return;
            }

            if (methodName.StartsWith("set_"))
            {
                var propertyName = methodName.Substring(4);
                SetProperty(target, propertyName, invocation);
            }
            else if (methodName.StartsWith("get_"))
            {
                var propertyName = methodName.Substring(4);
                GetProperty(target, propertyName, invocation);
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
