# Porting from Java Orbit

CSorbit is a C# port of [Orbit](https://github.com/MeteorDevelopment/orbit), the Java event bus by Meteor Development. The reference source declares version `0.3.0` in `build.gradle`. Its exact upstream commit is unknown; see [Third-party notices](THIRD_PARTY_NOTICES.md) for attribution and licensing.

The library uses the `CSorbit` namespace and builds as `CSorbit.dll` for .NET Standard 2.0, .NET Standard 2.1, and .NET 8. It has no dependency on Godot or Unity APIs.

## API mapping

| Java Orbit | CSorbit |
| --- | --- |
| `IEventBus`, `EventBus` | `IEventBus`, `EventBus` |
| `@EventHandler(priority = ...)` | `[EventHandler(...)]` or `[EventHandler(Priority = ...)]` |
| `EventPriority.HIGHEST/HIGH/MEDIUM/LOW/LOWEST` | `EventPriority.Highest/High/Medium/Low/Lowest` |
| `post(event)` | `Post(event)` |
| `subscribe(object / Class / IListener)` | `Subscribe(object / Type / IListener)` |
| `unsubscribe(...)` | `Unsubscribe(...)` |
| `isSubscribed(...)`, `isListening(Class)` | `IsSubscribed(...)`, `IsListening(Type)` / `IsListening<T>()` |
| `ICancellable.isCancelled()` / `setCancelled(bool)` | `ICancellable.IsCancelled { get; set; }` |
| `ConsumerListener<T>` | `CSorbit.Listeners.ConsumerListener<T>`, also returned by `Subscribe<T>(Action<T>, priority)` |
| `registerLookup(...)`, `NoLambdaFactoryException` | No C# equivalent; Java lookup registration is not required by .NET |

## Event delivery

- `Post` delivers events synchronously on the calling thread and returns the same event instance. A handler exception propagates to the caller and stops delivery.
- Listeners receive only the exact runtime event type, determined by `event.GetType()`. Base class and interface listeners do not receive derived event types automatically.
- Higher numeric priorities run first. Equal-priority listeners run in subscription order. Do not depend on the discovery order of attributed methods within a subscriber.
- Handler discovery walks the class hierarchy, including private methods. Subscribing an object registers its instance and static handlers; subscribing a `Type` registers only static handlers.
- An attributed handler must return `void` and accept one closed reference-type parameter. Generic methods and parameters using `ref`, `out`, or pointer types are ignored.
- Each `Post` uses a listener snapshot. Subscribing or unsubscribing during a handler affects later posts, while the current snapshot continues subject to cancellation or an exception. A nested `Post` takes a new snapshot.
- If listeners exist for a cancellable event, `IsCancelled` resets to `false` before delivery. Once a handler sets it to `true`, later handlers are skipped. An event with no listeners keeps its existing cancellation state.

## Differences from Java Orbit

### Subscription state

Repeatedly subscribing the same object, `Type`, or `IListener` instance does not add duplicate subscriptions. Java Orbit could add the same listeners multiple times. Objects and listeners are compared by reference identity, even if they override `Equals`.

`IsSubscribed` reports active subscription state. `Clear()` removes all subscriptions and cached handler metadata. After the last listener for an event type is removed, its entry is removed as well. Posting that event then leaves `IsCancelled` unchanged; Java Orbit could retain an empty entry and reset cancellation in this case.

### Cancellation and event types

Every `Post<T>(T)` checks whether the runtime event implements `ICancellable`. Cancellation therefore also works when the variable is declared as `object`. In Java, the selected `post` overload depended on the variable's declared type, so passing a cancellable event as `Object` could use ordinary delivery.

CSorbit also provides `CancellableEvent.Cancel()` and the `Cancel(this ICancellable)` extension method.

Events must be reference types. Java Orbit excluded primitive handler parameters; CSorbit excludes all .NET value types, including `struct` events. Public bus operations reject `null` arguments with `ArgumentNullException`.

Abstract attributed methods are ignored. For a virtual handler, apply `[EventHandler]` to the concrete implementation that should be discovered.

### Handler invocation

The default `ListenerInvocationMode.Delegate` binds attributed methods to typed delegates when subscribing. `ListenerInvocationMode.Reflection` invokes attributed methods through reflection and avoids creating generic delegates at runtime. Both modes propagate the original handler exception.

Explicit `Subscribe<T>(Action<T>)` calls use the supplied delegate in either mode. This is also an option for AOT applications that can register their handlers directly.

## Threading and game engines

Subscription storage is protected against concurrent changes, and handlers run outside its lock. Concurrent `Post` calls can execute handlers concurrently. CSorbit does not schedule calls on the main thread or synchronize application state. Avoid posting the same mutable event instance concurrently.

Post from the main thread when handlers access Godot or Unity APIs that require it. For Unity IL2CPP or trimmed .NET applications, preserve reflected handlers and validate the final application on the target platform. The [Unity example guide](../examples/Unity/README.md) describes invocation modes and code stripping.
