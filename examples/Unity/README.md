# Unity example

This example uses a cancellable damage event with an attributed guard handler and an explicit delegate listener.

## Installation

1. Download and extract `CSorbit-unity.zip` from a GitHub release, or build the library using the instructions in the [main README](../../README.md). A local build places the Unity DLL at `artifacts/unity/CSorbit.dll`.
2. Copy `CSorbit.dll` to `Assets/Plugins/CSorbit/` in your Unity project. The Unity package targets .NET Standard 2.0.
3. In **Player Settings**, select a compatible API profile: **.NET Standard 2.0**, **.NET Standard 2.1**, or a compatible **.NET Framework 4.x** profile, depending on the profiles your Unity version provides.
4. Copy [CSorbitDemoEventListener.cs](CSorbitDemoEventListener.cs) and [link.xml](link.xml) to `Assets/CSorbitExample/`.
5. Attach the `CSorbitDemoEventListener` component to one GameObject and enter Play mode.

The Console should show:

```text
Damage applied: 10
Guard cancelled damage: 100
Cancelled: True
```

The high-priority guard cancels damage above 50 before the ordinary listener runs.

## Subscription lifetime

The component subscribes in `OnEnable` and unsubscribes in `OnDisable`. It stores the `ConsumerListener<DamageEvent>` returned by the delegate subscription so that listener can also be removed. Use this pattern to prevent disabled or destroyed components from retaining subscriptions.

The example shares a static bus. A game can instead expose a shared bus through its own service. Events run synchronously on the thread calling `Post`; call it on Unity's main thread when handlers access Unity APIs.

## IL2CPP and managed code stripping

The example constructs its bus with:

```csharp
var bus = new EventBus(ListenerInvocationMode.Reflection);
```

Reflection mode avoids creating generic delegates at runtime for attributed handlers. Preserve those handlers and their event types using `[Preserve]` and/or `link.xml`, because the linker may not detect reflection access.

The supplied `link.xml` preserves CSorbit and this example's types in `Assembly-CSharp`. If your scripts belong to an `.asmdef` assembly, replace `Assembly-CSharp` with that assembly's name and list your own subscriber and event types.

For explicit registration, use `bus.Subscribe<MyEvent>(OnMyEvent)` and retain the returned `ConsumerListener<MyEvent>` for `bus.Unsubscribe(listener)`. This uses a statically known generic type and delegate without attributed handler discovery.

IL2CPP behavior also depends on your application types and stripping settings. Test the final Player build on the target platform. The repository's automated checks do not run the Unity Editor or an IL2CPP Player.
