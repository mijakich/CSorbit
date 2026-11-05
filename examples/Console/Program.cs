using System;
using CSorbit;

namespace CSorbitConsoleExample
{
    internal static class Program
    {
        private static void Main()
        {
            var bus = new EventBus();
            var guard = new DamageGuard();
            bus.Subscribe(guard);
            var listener = bus.Subscribe<DamageEvent>(
                value => Console.WriteLine("Damage applied: " + value.Amount));

            Console.WriteLine("Allowed request:");
            bus.Post(new DamageEvent(10));
            Console.WriteLine("Cancelled request:");
            var denied = bus.Post(new DamageEvent(100));
            Console.WriteLine("IsCancelled: " + denied.IsCancelled);

            bus.Unsubscribe(listener);
            bus.Unsubscribe(guard);
            Console.WriteLine("IsListening: " + bus.IsListening<DamageEvent>());
        }
    }

    internal sealed class DamageEvent : CancellableEvent
    {
        public int Amount { get; }
        public DamageEvent(int amount) { Amount = amount; }
    }

    internal sealed class DamageGuard
    {
        [EventHandler(EventPriority.High)]
        private void OnDamage(DamageEvent value)
        {
            if (value.Amount > 50)
            {
                Console.WriteLine("Guard cancelled damage: " + value.Amount);
                value.Cancel();
            }
        }
    }
}
