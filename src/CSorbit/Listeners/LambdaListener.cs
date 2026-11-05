using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace CSorbit.Listeners
{
    /// <summary>Binds an attributed instance or static method to an event listener.</summary>
    public sealed class LambdaListener : IListener
    {
        private static readonly MethodInfo ExecutorFactory = typeof(LambdaListener).GetMethod(
            nameof(CreateExecutor), BindingFlags.NonPublic | BindingFlags.Static);
        private readonly Action<object> executor;

        public Type Target { get; private set; }
        public int Priority { get; private set; }
        public bool IsStatic { get; private set; }

        public LambdaListener(object subscriber, MethodInfo method,
            ListenerInvocationMode invocationMode = ListenerInvocationMode.Delegate)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (!IsValidHandler(method))
                throw new ArgumentException("Expected an attributed void method with one reference-type event parameter.", nameof(method));
            if (invocationMode != ListenerInvocationMode.Delegate && invocationMode != ListenerInvocationMode.Reflection)
                throw new ArgumentOutOfRangeException(nameof(invocationMode));
            if (!method.IsStatic)
            {
                if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
                if (!method.DeclaringType.IsInstanceOfType(subscriber))
                    throw new ArgumentException("The subscriber does not match the method's declaring type.", nameof(subscriber));
            }

            Target = method.GetParameters()[0].ParameterType;
            Priority = method.GetCustomAttribute<EventHandlerAttribute>(false).Priority;
            IsStatic = method.IsStatic;
            object target = IsStatic ? null : subscriber;

            if (invocationMode == ListenerInvocationMode.Reflection)
            {
                executor = value => InvokeUnwrapped(method, target, new[] { value });
            }
            else
            {
                executor = (Action<object>)InvokeUnwrapped(ExecutorFactory.MakeGenericMethod(Target),
                    null, new object[] { target, method });
            }
        }

        public void Call(object value)
        {
            executor(value);
        }

        internal static bool IsValidHandler(MethodInfo method)
        {
            if (!method.IsDefined(typeof(EventHandlerAttribute), false)
                || method.ReturnType != typeof(void) || method.ContainsGenericParameters || method.IsAbstract)
                return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1) return false;
            Type eventType = parameters[0].ParameterType;
            return !eventType.IsValueType && !eventType.IsByRef && !eventType.IsPointer
                && !eventType.ContainsGenericParameters;
        }

        private static Action<object> CreateExecutor<T>(object subscriber, MethodInfo method) where T : class
        {
            Action<T> callback = (Action<T>)(method.IsStatic
                ? System.Delegate.CreateDelegate(typeof(Action<T>), method, true)
                : System.Delegate.CreateDelegate(typeof(Action<T>), subscriber, method, true));
            return value => callback((T)value);
        }

        private static object InvokeUnwrapped(MethodInfo method, object subscriber, object[] arguments)
        {
            try
            {
                return method.Invoke(subscriber, arguments);
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException == null) throw;
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
