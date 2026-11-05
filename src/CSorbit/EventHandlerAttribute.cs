using System;

namespace CSorbit
{
    /// <summary>Marks a void method accepting one reference-type event as a listener.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class EventHandlerAttribute : Attribute
    {
        public int Priority { get; set; }

        public EventHandlerAttribute(int priority = EventPriority.Medium)
        {
            Priority = priority;
        }
    }
}
