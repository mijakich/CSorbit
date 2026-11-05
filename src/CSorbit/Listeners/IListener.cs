using System;

namespace CSorbit.Listeners
{
    /// <summary>A listener's target and priority must remain constant while subscribed.</summary>
    public interface IListener
    {
        Type Target { get; }
        int Priority { get; }
        void Call(object value);
    }
}
