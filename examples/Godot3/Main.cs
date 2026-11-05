using Godot;
using CSorbit;

public class Main : Node
{
	private readonly EventBus bus = new EventBus();
	private int appliedCount;

	public override void _Ready()
	{
		bus.Subscribe(this);
		bus.Post(new DamageEvent(10));
		var denied = bus.Post(new DamageEvent(100));
		GD.Print("Cancelled: ", denied.IsCancelled);
		if (appliedCount != 1 || !denied.IsCancelled)
			throw new System.InvalidOperationException("CSorbit example validation failed.");

		if (System.Array.IndexOf(OS.GetCmdlineArgs(), "--csorbit-smoke-test") >= 0)
		{
			GD.Print("CSORBIT_GODOT3_OK");
			GetTree().Quit();
		}
	}

	public override void _ExitTree()
	{
		bus.Unsubscribe(this);
	}

	[EventHandler(EventPriority.High)]
	private void GuardDamage(DamageEvent value)
	{
		if (value.Amount > 50)
		{
			GD.Print("Guard cancelled damage: ", value.Amount);
			value.Cancel();
		}
	}

	[EventHandler]
	private void ApplyDamage(DamageEvent value)
	{
		appliedCount++;
		GD.Print("Damage applied: ", value.Amount);
	}

	private sealed class DamageEvent : CancellableEvent
	{
		public int Amount { get; }
		public DamageEvent(int amount) { Amount = amount; }
	}
}
