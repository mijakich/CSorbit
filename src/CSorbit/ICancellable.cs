using System;

namespace CSorbit
{
    /// <summary>Allows an event to stop delivery to the remaining listeners.</summary>
    public interface ICancellable
    {
        bool IsCancelled { get; set; }
    }

    public static class CancellableExtensions
    {
        public static void Cancel(this ICancellable value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            value.IsCancelled = true;
        }
    }

    /// <summary>Optional base class for cancellable events.</summary>
    public abstract class CancellableEvent : ICancellable
    {
        public bool IsCancelled { get; set; }

        public void Cancel()
        {
            IsCancelled = true;
        }
    }
}
