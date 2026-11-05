using CSorbit;
using CSorbit.Listeners;
using UnityEngine;
using UnityEngine.Scripting;

namespace CSorbitUnityExample
{
    [Preserve]
    public sealed class CSorbitDemoEventListener : MonoBehaviour
    {
        // Reflection mode avoids runtime generic delegate construction in IL2CPP.
        private static readonly EventBus Bus = new EventBus(ListenerInvocationMode.Reflection);
        private ConsumerListener<DamageEvent> appliedListener;

        private void OnEnable()
        {
            Bus.Subscribe(this);
            appliedListener = Bus.Subscribe<DamageEvent>(
                value => Debug.Log("Damage applied: " + value.Amount));
        }

        private void Start()
        {
            Bus.Post(new DamageEvent(10));
            var denied = Bus.Post(new DamageEvent(100));
            Debug.Log("Cancelled: " + denied.IsCancelled);
        }

        private void OnDisable()
        {
            Bus.Unsubscribe(this);
            if (appliedListener != null)
            {
                Bus.Unsubscribe(appliedListener);
                appliedListener = null;
            }
        }

        [Preserve]
        [EventHandler(EventPriority.High)]
        private void GuardDamage(DamageEvent value)
        {
            if (value.Amount > 50)
            {
                Debug.Log("Guard cancelled damage: " + value.Amount);
                value.Cancel();
            }
        }

        [Preserve]
        private sealed class DamageEvent : CancellableEvent
        {
            public int Amount { get; }
            public DamageEvent(int amount) { Amount = amount; }
        }
    }
}
