using System;
using System.Collections.Generic;
using System.Reflection;
using CSorbit.Listeners;

namespace CSorbit
{
    /// <summary>
    /// Thread-safe subscription storage with synchronous, snapshot-based delivery.
    /// Handlers run on the thread calling Post, outside the subscription lock.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly object gate = new object();
        private readonly Dictionary<object, IListener[]> instanceListeners =
            new Dictionary<object, IListener[]>(ReferenceIdentityComparer.Instance);
        private readonly Dictionary<Type, IListener[]> staticListeners = new Dictionary<Type, IListener[]>();
        private readonly Dictionary<Type, IListener[]> listenersByEvent = new Dictionary<Type, IListener[]>();
        private readonly Dictionary<Type, MethodInfo[]> handlerCache = new Dictionary<Type, MethodInfo[]>();
        private readonly ListenerInvocationMode invocationMode;

        public EventBus(ListenerInvocationMode invocationMode = ListenerInvocationMode.Delegate)
        {
            if (invocationMode != ListenerInvocationMode.Delegate && invocationMode != ListenerInvocationMode.Reflection)
                throw new ArgumentOutOfRangeException(nameof(invocationMode));
            this.invocationMode = invocationMode;
        }

        public bool IsListening(Type eventType)
        {
            if (eventType == null) throw new ArgumentNullException(nameof(eventType));
            lock (gate) return listenersByEvent.ContainsKey(eventType);
        }

        public bool IsListening<T>() where T : class
        {
            return IsListening(typeof(T));
        }

        public bool IsSubscribed(object subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            Type type = subscriber as Type;
            if (type != null) return IsSubscribed(type);
            IListener listener = subscriber as IListener;
            if (listener != null) return IsSubscribed(listener);
            lock (gate) return instanceListeners.ContainsKey(subscriber);
        }

        public bool IsSubscribed(Type subscriberType)
        {
            if (subscriberType == null) throw new ArgumentNullException(nameof(subscriberType));
            lock (gate) return staticListeners.ContainsKey(subscriberType);
        }

        public bool IsSubscribed(IListener listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            lock (gate)
            {
                IListener[] listeners;
                return listenersByEvent.TryGetValue(listener.Target, out listeners) && IndexOf(listeners, listener) >= 0;
            }
        }

        /// <summary>
        /// Delivers to the exact runtime type and returns the same event.
        /// Cancellable events reset only when listeners exist; cancellation stops delivery.
        /// </summary>
        public T Post<T>(T value) where T : class
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            IListener[] listeners;
            lock (gate)
            {
                if (!listenersByEvent.TryGetValue(value.GetType(), out listeners)) return value;
            }

            ICancellable cancellable = value as ICancellable;
            if (cancellable != null) cancellable.IsCancelled = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                listeners[i].Call(value);
                if (cancellable != null && cancellable.IsCancelled) break;
            }
            return value;
        }

        public void Subscribe(object subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            Type type = subscriber as Type;
            if (type != null) { Subscribe(type); return; }
            IListener listener = subscriber as IListener;
            if (listener != null) { Subscribe(listener); return; }
            lock (gate)
            {
                if (instanceListeners.ContainsKey(subscriber)) return;
                IListener[] listeners = CreateListeners(subscriber.GetType(), subscriber);
                instanceListeners.Add(subscriber, listeners);
                for (int i = 0; i < listeners.Length; i++) AddListener(listeners[i]);
            }
        }

        public void Subscribe(Type subscriberType)
        {
            ValidateSubscriberType(subscriberType);
            lock (gate)
            {
                if (staticListeners.ContainsKey(subscriberType)) return;
                IListener[] listeners = CreateListeners(subscriberType, null);
                staticListeners.Add(subscriberType, listeners);
                for (int i = 0; i < listeners.Length; i++) AddListener(listeners[i]);
            }
        }

        public void Subscribe(IListener listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            Type target = listener.Target;
            if (target == null || target.IsValueType || target.IsByRef || target.IsPointer || target.ContainsGenericParameters)
                throw new ArgumentException("Listener target must be a closed reference type.", nameof(listener));
            lock (gate) AddListener(listener);
        }

        public ConsumerListener<T> Subscribe<T>(Action<T> executor, int priority = EventPriority.Medium) where T : class
        {
            var listener = new ConsumerListener<T>(executor, priority);
            Subscribe(listener);
            return listener;
        }

        public void Unsubscribe(object subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            Type type = subscriber as Type;
            if (type != null) { Unsubscribe(type); return; }
            IListener listener = subscriber as IListener;
            if (listener != null) { Unsubscribe(listener); return; }
            lock (gate)
            {
                IListener[] listeners;
                if (instanceListeners.TryGetValue(subscriber, out listeners))
                {
                    instanceListeners.Remove(subscriber);
                    for (int i = 0; i < listeners.Length; i++) RemoveListener(listeners[i]);
                }
                else
                {
                    // Orbit's backwards-compatible fallback for a static subscription.
                    Unsubscribe(subscriber.GetType());
                }
            }
        }

        public void Unsubscribe(Type subscriberType)
        {
            if (subscriberType == null) throw new ArgumentNullException(nameof(subscriberType));
            lock (gate)
            {
                IListener[] listeners;
                if (!staticListeners.TryGetValue(subscriberType, out listeners)) return;
                staticListeners.Remove(subscriberType);
                for (int i = 0; i < listeners.Length; i++) RemoveListener(listeners[i]);
            }
        }

        public void Unsubscribe(IListener listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            lock (gate) RemoveListener(listener);
        }

        /// <summary>Removes all subscriptions and cached handler metadata.</summary>
        public void Clear()
        {
            lock (gate)
            {
                instanceListeners.Clear();
                staticListeners.Clear();
                listenersByEvent.Clear();
                handlerCache.Clear();
            }
        }

        private IListener[] CreateListeners(Type subscriberType, object subscriber)
        {
            ValidateSubscriberType(subscriberType);
            MethodInfo[] methods;
            if (!handlerCache.TryGetValue(subscriberType, out methods))
            {
                var found = new List<MethodInfo>();
                for (Type type = subscriberType; type != null && type != typeof(object); type = type.BaseType)
                {
                    MethodInfo[] declared = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    foreach (MethodInfo method in declared)
                        if (LambdaListener.IsValidHandler(method)) found.Add(method);
                }
                methods = found.ToArray();
                handlerCache.Add(subscriberType, methods);
            }

            var listeners = new List<IListener>();
            foreach (MethodInfo method in methods)
                if (subscriber != null || method.IsStatic)
                    listeners.Add(new LambdaListener(subscriber, method, invocationMode));
            return listeners.ToArray();
        }

        private void AddListener(IListener listener)
        {
            IListener[] current;
            if (!listenersByEvent.TryGetValue(listener.Target, out current)) current = new IListener[0];
            if (IndexOf(current, listener) >= 0) return;

            int insertion = 0;
            while (insertion < current.Length && listener.Priority <= current[insertion].Priority) insertion++;
            var updated = new IListener[current.Length + 1];
            Array.Copy(current, 0, updated, 0, insertion);
            updated[insertion] = listener;
            Array.Copy(current, insertion, updated, insertion + 1, current.Length - insertion);
            listenersByEvent[listener.Target] = updated;
        }

        private void RemoveListener(IListener listener)
        {
            IListener[] current;
            if (!listenersByEvent.TryGetValue(listener.Target, out current)) return;
            int index = IndexOf(current, listener);
            if (index < 0) return;
            if (current.Length == 1) { listenersByEvent.Remove(listener.Target); return; }
            var updated = new IListener[current.Length - 1];
            Array.Copy(current, 0, updated, 0, index);
            Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
            listenersByEvent[listener.Target] = updated;
        }

        private static int IndexOf(IListener[] listeners, IListener listener)
        {
            for (int i = 0; i < listeners.Length; i++)
                if (ReferenceEquals(listeners[i], listener)) return i;
            return -1;
        }

        private static void ValidateSubscriberType(Type subscriberType)
        {
            if (subscriberType == null) throw new ArgumentNullException(nameof(subscriberType));
            if (subscriberType.IsValueType || subscriberType.ContainsGenericParameters)
                throw new ArgumentException("Subscriber must be a closed reference type.", nameof(subscriberType));
        }
    }
}
