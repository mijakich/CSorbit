using System;

namespace CSorbit.Listeners
{
    /// <summary>An explicitly typed callback, also suitable for AOT builds.</summary>
    public sealed class ConsumerListener<T> : IListener where T : class
    {
        private readonly Action<T> executor;

        public Type Target { get { return typeof(T); } }
        public int Priority { get; private set; }

        public ConsumerListener(Action<T> executor, int priority = EventPriority.Medium)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            this.executor = executor;
            Priority = priority;
        }

        public ConsumerListener(int priority, Action<T> executor) : this(executor, priority) { }

        public void Call(object value)
        {
            executor((T)value);
        }
    }
}
