using System;
using CSorbit.Listeners;

namespace CSorbit
{
    /// <summary>Synchronous event delivery for the exact runtime event type.</summary>
    public interface IEventBus
    {
        bool IsListening(Type eventType);
        bool IsListening<T>() where T : class;
        bool IsSubscribed(object subscriber);
        bool IsSubscribed(Type subscriberType);
        bool IsSubscribed(IListener listener);
        T Post<T>(T value) where T : class;
        void Subscribe(object subscriber);
        void Subscribe(Type subscriberType);
        void Subscribe(IListener listener);
        ConsumerListener<T> Subscribe<T>(Action<T> executor, int priority = EventPriority.Medium) where T : class;
        void Unsubscribe(object subscriber);
        void Unsubscribe(Type subscriberType);
        void Unsubscribe(IListener listener);
        void Clear();
    }
}
