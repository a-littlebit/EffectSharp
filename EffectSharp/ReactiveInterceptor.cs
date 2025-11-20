using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

            if (!targetMethod.IsSpecialName)
            {
                if (targetMethod.DeclaringType == typeof(IReactive) && targetMethod.Name == nameof(IReactive.GetDependency))
                {
                    string propertyName = invocation.GetArgumentValue(0) as string;
                    if (propertyName == null)
                    {
                        throw new ArgumentNullException("Property name cannot be null.");
                    }
                    invocation.ReturnValue = DependencyTracker.GetDependency(target, propertyName);
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
                invocation.Proceed();
                if (PropertyChanged != null)
                {
                    DependencyTracker.EnqueueNotify(this, propertyName, (e) =>
                    {
                        PropertyChanged?.Invoke(this, e);
                    });
                }
                DependencyTracker.TriggerDependency(target, propertyName);
            }
            else if (methodName.StartsWith("get_"))
            {
                var propertyName = methodName.Substring(4);
                DependencyTracker.TrackDependency(target, propertyName);
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
    }
}
