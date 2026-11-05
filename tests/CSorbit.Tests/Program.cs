using System.Reflection;
using CSorbit;
using CSorbit.Listeners;

// These tests intentionally use no test framework, engine, or NuGet dependency.
// Run with: dotnet run --project tests/Orbit.Tests -c Release
internal static class Program
{
    private static ListenerInvocationMode currentMode;
    private static EventBus CreateBus() => new EventBus(currentMode);

    private static int Main()
    {
        (string Name, Action Run)[] tests =
        {
            ("Post returns the same event with and without listeners", PostReturnsSameEvent),
            ("Dispatch uses the exact runtime event type", ExactRuntimeType),
            ("Priorities descend and preserve insertion order for ties", PriorityOrder),
            ("Cancellation resets and stops dispatch through object references", RuntimeCancellation),
            ("Cancellation remains unchanged when no listener exists", CancellationWithoutListeners),
            ("Custom ICancellable events support the Cancel extension", CustomCancellation),
            ("Object subscriptions are idempotent and removable", ObjectSubscription),
            ("Static subscriptions are idempotent and removable", StaticSubscription),
            ("Direct listener subscriptions are idempotent and removable", DirectSubscription),
            ("Unsubscribe(object) supports the legacy static fallback", StaticUnsubscribeFallback),
            ("Private inherited and static methods are discovered", InheritedHandlers),
            ("Static type subscriptions exclude instance methods", StaticHandlersOnly),
            ("Invalid attributed methods are skipped", InvalidHandlers),
            ("Unsubscribe during dispatch changes only subsequent posts", SnapshotUnsubscribe),
            ("Subscribe during dispatch changes only subsequent posts", SnapshotSubscribe),
            ("Nested posts see current subscriptions", ReentrantPost),
            ("Reflected instance exceptions propagate without wrapping", InstanceException),
            ("Reflected static exceptions propagate without wrapping", StaticException),
            ("Direct callback exceptions stop dispatch", CallbackException),
            ("Subscribers use reference identity despite overridden equality", SubscriberIdentity),
            ("Listeners use reference identity despite overridden equality", ListenerIdentity),
            ("Consumer listeners expose target and priority", ConsumerListenerApi),
            ("Lambda listeners support private instance and static methods", LambdaListenerApi),
            ("Public APIs reject null arguments", NullArguments),
            ("Concurrent subscribe, unsubscribe, and post preserve stable listeners", ConcurrentOperations),
            ("Clear releases subscriptions and leaves existing snapshots valid", ClearSubscriptions),
            ("Type and listener subscriptions work through object references", ObjectOverloads),
            ("Invalid subscriber and listener types are rejected", InvalidTypes)
        };

        var failures = 0;
        foreach (var mode in new[] { ListenerInvocationMode.Delegate, ListenerInvocationMode.Reflection })
        {
            currentMode = mode;
            foreach (var test in tests)
            {
                try
                {
                    test.Run();
                    Console.WriteLine($"PASS [{mode}] {test.Name}");
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"FAIL [{mode}] {test.Name}\n{exception}");
                }
            }
        }

        Console.WriteLine($"{tests.Length * 2 - failures}/{tests.Length * 2} tests passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void PostReturnsSameEvent()
    {
        IEventBus bus = CreateBus();
        var value = new Message();
        Same(value, bus.Post(value));
        var called = false;
        bus.Subscribe(new ConsumerListener<Message>(message =>
        {
            Same(value, message);
            called = true;
        }));
        Same(value, bus.Post(value));
        True(called);
    }

    private static void ExactRuntimeType()
    {
        var bus = CreateBus();
        var calls = new List<string>();
        bus.Subscribe<object>(_ => calls.Add("object"));
        bus.Subscribe<BaseMessage>(_ => calls.Add("base"));
        bus.Subscribe<IMessage>(_ => calls.Add("interface"));
        bus.Subscribe<Message>(_ => calls.Add("message"));
        object value = new Message();
        bus.Post(value);
        Equal("message", string.Join(",", calls));
        True(bus.IsListening(typeof(Message)));
        True(bus.IsListening<Message>());
        False(bus.IsListening(typeof(UnhandledMessage)));
    }

    private static void PriorityOrder()
    {
        var bus = CreateBus();
        var calls = new List<string>();
        bus.Subscribe<Message>(_ => calls.Add("medium-first"));
        bus.Subscribe<Message>(_ => calls.Add("lowest"), int.MinValue);
        bus.Subscribe<Message>(_ => calls.Add("high-first"), EventPriority.High);
        bus.Subscribe<Message>(_ => calls.Add("medium-second"), EventPriority.Medium);
        bus.Subscribe<Message>(_ => calls.Add("highest"), int.MaxValue);
        bus.Subscribe<Message>(_ => calls.Add("high-second"), EventPriority.High);
        bus.Post(new Message());
        Equal("highest,high-first,high-second,medium-first,medium-second,lowest", string.Join(",", calls));
        Equal(200, EventPriority.Highest);
        Equal(100, EventPriority.High);
        Equal(0, EventPriority.Medium);
        Equal(-100, EventPriority.Low);
        Equal(-200, EventPriority.Lowest);
    }

    private static void RuntimeCancellation()
    {
        var bus = CreateBus();
        var calls = new List<string>();
        bus.Subscribe<CancelledMessage>(message =>
        {
            False(message.IsCancelled, "Cancellation must reset before the first callback.");
            calls.Add("cancel");
            message.Cancel();
        }, EventPriority.High);
        bus.Subscribe<CancelledMessage>(_ => calls.Add("after"));
        object value = new CancelledMessage { IsCancelled = true };
        Same(value, bus.Post(value));
        Same(value, bus.Post(value));
        Equal("cancel,cancel", string.Join(",", calls));
        True(((ICancellable)value).IsCancelled);
    }

    private static void CancellationWithoutListeners()
    {
        var bus = CreateBus();
        var value = new CancelledMessage { IsCancelled = true };
        bus.Post(value);
        True(value.IsCancelled);
        var listener = bus.Subscribe<CancelledMessage>(_ => { });
        bus.Post(value);
        False(value.IsCancelled);
        bus.Unsubscribe(listener);
        value.IsCancelled = true;
        bus.Post(value);
        True(value.IsCancelled, "An empty listener collection must not reset cancellation.");
    }

    private static void CustomCancellation()
    {
        var bus = CreateBus();
        var calls = 0;
        bus.Subscribe<CustomCancelledMessage>(message =>
        {
            False(message.IsCancelled);
            message.Cancel();
            calls++;
        }, EventPriority.Highest);
        bus.Subscribe<CustomCancelledMessage>(_ => calls++);
        ICancellable value = new CustomCancelledMessage { IsCancelled = true };
        bus.Post(value);
        Equal(1, calls);
        True(value.IsCancelled);
    }

    private static void ObjectSubscription()
    {
        var bus = CreateBus();
        var owner = new InstanceOwner();
        False(bus.IsSubscribed(owner));
        bus.Subscribe(owner);
        bus.Subscribe(owner);
        True(bus.IsSubscribed(owner));
        bus.Post(new Message());
        Equal(1, owner.Calls);
        bus.Unsubscribe(owner);
        bus.Unsubscribe(owner);
        False(bus.IsSubscribed(owner));
        False(bus.IsListening<Message>());
        bus.Post(new Message());
        Equal(1, owner.Calls);
        bus.Subscribe(owner);
        bus.Post(new Message());
        Equal(2, owner.Calls);
    }

    private static void StaticSubscription()
    {
        StaticOwner.Calls = 0;
        var bus = CreateBus();
        bus.Subscribe(typeof(StaticOwner));
        bus.Subscribe(typeof(StaticOwner));
        True(bus.IsSubscribed(typeof(StaticOwner)));
        bus.Post(new Message());
        Equal(1, StaticOwner.Calls);
        bus.Unsubscribe(typeof(StaticOwner));
        False(bus.IsSubscribed(typeof(StaticOwner)));
        False(bus.IsListening<Message>());
        bus.Post(new Message());
        Equal(1, StaticOwner.Calls);
    }

    private static void DirectSubscription()
    {
        var bus = CreateBus();
        var calls = 0;
        IListener listener = new ConsumerListener<Message>(_ => calls++);
        bus.Subscribe(listener);
        bus.Subscribe(listener);
        True(bus.IsSubscribed(listener));
        bus.Post(new Message());
        Equal(1, calls);
        bus.Unsubscribe(listener);
        bus.Unsubscribe(listener);
        False(bus.IsSubscribed(listener));
        False(bus.IsListening<Message>());
        bus.Subscribe(listener);
        bus.Post(new Message());
        Equal(2, calls);
    }

    private static void StaticUnsubscribeFallback()
    {
        StaticOwner.Calls = 0;
        var bus = CreateBus();
        bus.Subscribe(typeof(StaticOwner));
        bus.Unsubscribe(new StaticOwner());
        False(bus.IsSubscribed(typeof(StaticOwner)));
        bus.Post(new Message());
        Equal(0, StaticOwner.Calls);
    }

    private static void InheritedHandlers()
    {
        ParentOwner.Calls.Clear();
        var bus = CreateBus();
        var owner = new ChildOwner();
        bus.Subscribe(owner);
        bus.Post(new Message());
        Equal("child,parent,parent-static", string.Join(",", ParentOwner.Calls));
        bus.Unsubscribe(owner);
        bus.Post(new Message());
        Equal(3, ParentOwner.Calls.Count);
    }

    private static void StaticHandlersOnly()
    {
        ParentOwner.Calls.Clear();
        var bus = CreateBus();
        bus.Subscribe(typeof(ChildOwner));
        bus.Post(new Message());
        Equal("parent-static", string.Join(",", ParentOwner.Calls));
        bus.Unsubscribe(typeof(ChildOwner));
        False(bus.IsListening<Message>());
    }

    private static void InvalidHandlers()
    {
        var bus = CreateBus();
        var owner = new InvalidOwner();
        bus.Subscribe(owner);
        bus.Post(new Message());
        Equal(1, owner.Calls);
        False(bus.IsListening(typeof(int)));
        False(bus.IsListening(typeof(ValueMessage)));
        False(bus.IsListening(typeof(Message).MakeByRefType()));
    }

    private static void SnapshotUnsubscribe()
    {
        var bus = CreateBus();
        var calls = new List<string>();
        var later = new ConsumerListener<Message>(_ => calls.Add("later"));
        bus.Subscribe<Message>(_ =>
        {
            calls.Add("first");
            bus.Unsubscribe(later);
        }, EventPriority.High);
        bus.Subscribe(later);
        bus.Post(new Message());
        bus.Post(new Message());
        Equal("first,later,first", string.Join(",", calls));
    }

    private static void SnapshotSubscribe()
    {
        var bus = CreateBus();
        var calls = new List<string>();
        var later = new ConsumerListener<Message>(_ => calls.Add("later"));
        bus.Subscribe<Message>(_ =>
        {
            calls.Add("first");
            bus.Subscribe(later);
        }, EventPriority.High);
        bus.Post(new Message());
        bus.Post(new Message());
        Equal("first,first,later", string.Join(",", calls));
    }

    private static void ReentrantPost()
    {
        var bus = CreateBus();
        var calls = new List<string>();
        var nested = false;
        var added = new ConsumerListener<Message>(_ => calls.Add("added"));
        bus.Subscribe<Message>(_ =>
        {
            calls.Add(nested ? "nested-first" : "outer-first");
            if (!nested)
            {
                nested = true;
                bus.Subscribe(added);
                bus.Post(new Message());
                nested = false;
            }
        }, EventPriority.High);
        bus.Subscribe<Message>(_ => calls.Add(nested ? "nested-last" : "outer-last"), EventPriority.Low);
        bus.Post(new Message());
        Equal("outer-first,nested-first,added,nested-last,outer-last", string.Join(",", calls));
    }

    private static void InstanceException()
    {
        var bus = CreateBus();
        var owner = new ThrowingOwner();
        var laterCalled = false;
        bus.Subscribe(owner);
        bus.Subscribe<Message>(_ => laterCalled = true, EventPriority.Low);
        Same(owner.Failure, Throws<TestFailure>(() => bus.Post(new Message())));
        False(laterCalled);
    }

    private static void StaticException()
    {
        var bus = CreateBus();
        bus.Subscribe(typeof(StaticThrowingOwner));
        Same(StaticThrowingOwner.Failure, Throws<TestFailure>(() => bus.Post(new Message())));
    }

    private static void CallbackException()
    {
        var bus = CreateBus();
        var failure = new TestFailure();
        var laterCalled = false;
        bus.Subscribe<Message>(_ => throw failure, EventPriority.High);
        bus.Subscribe<Message>(_ => laterCalled = true);
        Same(failure, Throws<TestFailure>(() => bus.Post(new Message())));
        False(laterCalled);
    }

    private static void SubscriberIdentity()
    {
        var bus = CreateBus();
        var first = new EqualOwner();
        var second = new EqualOwner();
        True(first.Equals(second));
        bus.Subscribe(first);
        False(bus.IsSubscribed(second));
        bus.Subscribe(second);
        bus.Post(new Message());
        Equal(1, first.Calls);
        Equal(1, second.Calls);
        bus.Unsubscribe(first);
        True(bus.IsSubscribed(second));
        bus.Post(new Message());
        Equal(1, first.Calls);
        Equal(2, second.Calls);
    }

    private static void ListenerIdentity()
    {
        var bus = CreateBus();
        IListener first = new EqualListener();
        IListener second = new EqualListener();
        bus.Subscribe(first);
        False(bus.IsSubscribed(second));
        bus.Subscribe(second);
        bus.Post(new Message());
        Equal(1, ((EqualListener)first).Calls);
        Equal(1, ((EqualListener)second).Calls);
        bus.Unsubscribe(first);
        False(bus.IsSubscribed(first));
        True(bus.IsSubscribed(second));
        bus.Post(new Message());
        Equal(1, ((EqualListener)first).Calls);
        Equal(2, ((EqualListener)second).Calls);
    }

    private static void ConsumerListenerApi()
    {
        var value = new Message();
        Message? received = null;
        var first = new ConsumerListener<Message>(message => received = message);
        Equal(typeof(Message), first.Target);
        Equal(EventPriority.Medium, first.Priority);
        first.Call(value);
        Same(value, received);
        var second = new ConsumerListener<Message>(EventPriority.Low, _ => { });
        Equal(EventPriority.Low, second.Priority);
        var third = new ConsumerListener<Message>(_ => { }, EventPriority.High);
        Equal(EventPriority.High, third.Priority);
        var bus = CreateBus();
        var convenient = bus.Subscribe<Message>(_ => { }, 17);
        Equal(17, convenient.Priority);
        True(bus.IsSubscribed(convenient));
        bus.Unsubscribe(convenient);
        False(bus.IsListening<Message>());
    }

    private static void LambdaListenerApi()
    {
        var owner = new LambdaOwner();
        var instance = new LambdaListener(owner, Method(typeof(LambdaOwner), "Instance"), currentMode);
        False(instance.IsStatic);
        Equal(typeof(Message), instance.Target);
        Equal(123, instance.Priority);
        var value = new Message();
        instance.Call(value);
        Same(value, owner.Received);
        LambdaOwner.StaticReceived = null;
        var staticListener = new LambdaListener(null!, Method(typeof(LambdaOwner), "Static"), currentMode);
        True(staticListener.IsStatic);
        Equal(EventPriority.Low, staticListener.Priority);
        staticListener.Call(value);
        Same(value, LambdaOwner.StaticReceived);
    }

    private static void NullArguments()
    {
        var bus = CreateBus();
        Throws<ArgumentNullException>(() => bus.Post<object>(null!));
        Throws<ArgumentNullException>(() => bus.Subscribe((object)null!));
        Throws<ArgumentNullException>(() => bus.Subscribe((Type)null!));
        Throws<ArgumentNullException>(() => bus.Subscribe((IListener)null!));
        Throws<ArgumentNullException>(() => bus.Unsubscribe((object)null!));
        Throws<ArgumentNullException>(() => bus.Unsubscribe((Type)null!));
        Throws<ArgumentNullException>(() => bus.Unsubscribe((IListener)null!));
        Throws<ArgumentNullException>(() => bus.IsSubscribed((object)null!));
        Throws<ArgumentNullException>(() => bus.IsSubscribed((Type)null!));
        Throws<ArgumentNullException>(() => bus.IsSubscribed((IListener)null!));
        Throws<ArgumentNullException>(() => bus.IsListening(null!));
        Throws<ArgumentNullException>(() => bus.Subscribe<Message>(null!));
        Throws<ArgumentNullException>(() => new ConsumerListener<Message>(null!));
        Throws<ArgumentNullException>(() => new LambdaListener(new LambdaOwner(), null!));
        Throws<ArgumentNullException>(() => new LambdaListener(null!, Method(typeof(LambdaOwner), "Instance")));
    }

    private static void ConcurrentOperations()
    {
        var bus = CreateBus();
        var calls = 0;
        var stable = new ConsumerListener<Message>(_ => Interlocked.Increment(ref calls));
        var changing = new ConsumerListener<Message>(_ => { });
        bus.Subscribe(stable);
        const int workers = 4;
        const int postsPerWorker = 2000;
        using var start = new ManualResetEventSlim(false);
        var posters = Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < postsPerWorker; i++) bus.Post(new Message());
        })).ToArray();
        var mutator = Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < postsPerWorker; i++)
            {
                bus.Subscribe(changing);
                bus.Subscribe(changing);
                bus.Unsubscribe(changing);
            }
        });
        start.Set();
        Task.WaitAll(posters.Append(mutator).ToArray());
        Equal(workers * postsPerWorker, calls);
        True(bus.IsSubscribed(stable));
        False(bus.IsSubscribed(changing));
    }

    private static void ClearSubscriptions()
    {
        var bus = CreateBus();
        var owner = new InstanceOwner();
        StaticOwner.Calls = 0;
        bus.Subscribe(owner);
        bus.Subscribe(typeof(StaticOwner));
        var clear = bus.Subscribe<Message>(_ => bus.Clear(), EventPriority.Highest);
        bus.Post(new Message());
        Equal(1, owner.Calls);
        Equal(1, StaticOwner.Calls);
        False(bus.IsSubscribed(owner));
        False(bus.IsSubscribed(typeof(StaticOwner)));
        False(bus.IsSubscribed(clear));
        False(bus.IsListening<Message>());
        bus.Post(new Message());
        Equal(1, owner.Calls);
        bus.Subscribe(owner);
        bus.Post(new Message());
        Equal(2, owner.Calls);
    }

    private static void ObjectOverloads()
    {
        var bus = CreateBus();
        StaticOwner.Calls = 0;
        object type = typeof(StaticOwner);
        object listener = new ConsumerListener<Message>(_ => { });
        bus.Subscribe(type);
        bus.Subscribe(listener);
        True(bus.IsSubscribed(type));
        True(bus.IsSubscribed(listener));
        bus.Post(new Message());
        Equal(1, StaticOwner.Calls);
        bus.Unsubscribe(type);
        bus.Unsubscribe(listener);
        False(bus.IsSubscribed(type));
        False(bus.IsSubscribed(listener));
        False(bus.IsListening<Message>());
    }

    private static void InvalidTypes()
    {
        var bus = CreateBus();
        Throws<ArgumentException>(() => bus.Subscribe(typeof(int)));
        Throws<ArgumentException>(() => bus.Subscribe(typeof(List<>)));
        Throws<ArgumentException>(() => bus.Subscribe(new InvalidListener()));
        Throws<ArgumentException>(() => new LambdaListener(new InvalidOwner(), Method(typeof(InvalidOwner), "NoArguments")));
        Throws<ArgumentException>(() => new LambdaListener(new object(), Method(typeof(LambdaOwner), "Instance")));
        Throws<ArgumentOutOfRangeException>(() => new EventBus((ListenerInvocationMode)99));
        Throws<ArgumentOutOfRangeException>(() => new LambdaListener(new LambdaOwner(), Method(typeof(LambdaOwner), "Instance"), (ListenerInvocationMode)99));
        Throws<ArgumentNullException>(() => CancellableExtensions.Cancel(null!));
        False(bus.IsListening<Message>());
    }

    private sealed class InvalidListener : IListener
    {
        public Type Target => typeof(int);
        public int Priority => 0;
        public void Call(object value) { }
    }

    private static MethodInfo Method(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing test method {type.Name}.{name}.");

    private static void True(bool condition, string? message = null)
    {
        if (!condition) throw new InvalidOperationException(message ?? "Expected true.");
    }

    private static void False(bool condition, string? message = null) => True(!condition, message ?? "Expected false.");

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected <{expected}>; actual <{actual}>.");
    }

    private static void Same(object? expected, object? actual)
    {
        if (!ReferenceEquals(expected, actual)) throw new InvalidOperationException("Expected the same object reference.");
    }

    private static TException Throws<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException exception) { return exception; }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Expected {typeof(TException).Name}, received {exception.GetType().Name}.", exception);
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    private interface IMessage { }
    private class BaseMessage { }
    private sealed class Message : BaseMessage, IMessage { }
    private sealed class UnhandledMessage { }
    private struct ValueMessage { }
    private sealed class CancelledMessage : CancellableEvent { }
    private sealed class CustomCancelledMessage : ICancellable { public bool IsCancelled { get; set; } }

    private class InstanceOwner
    {
        public int Calls;
        [EventHandler] private void OnMessage(Message message) => Calls++;
    }

    private sealed class StaticOwner
    {
        public static int Calls;
        [EventHandler] private static void OnMessage(Message message) => Calls++;
    }

    private class ParentOwner
    {
        public static readonly List<string> Calls = new();
        [EventHandler(EventPriority.Medium)] private void Parent(Message message) => Calls.Add("parent");
        [EventHandler(EventPriority.Low)] private static void ParentStatic(Message message) => Calls.Add("parent-static");
    }

    private sealed class ChildOwner : ParentOwner
    {
        [EventHandler(Priority = EventPriority.High)] private void Child(Message message) => Calls.Add("child");
    }

    private abstract class InvalidBase
    {
        [EventHandler] protected abstract void Abstract(Message message);
    }

    private sealed class InvalidOwner : InvalidBase
    {
        public int Calls;
        [EventHandler] private void Valid(Message message) => Calls++;
        private void Unattributed(Message message) => throw new TestFailure();
        [EventHandler] private void NoArguments() => throw new TestFailure();
        [EventHandler] private void TwoArguments(Message first, Message second) => throw new TestFailure();
        [EventHandler] private int NonVoid(Message message) => throw new TestFailure();
        [EventHandler] private void ValueType(int message) => throw new TestFailure();
        [EventHandler] private void Struct(ValueMessage message) => throw new TestFailure();
        [EventHandler] private void Generic<T>(Message message) => throw new TestFailure();
        [EventHandler] private void Ref(ref Message message) => throw new TestFailure();
        [EventHandler] private void Out(out Message message) => throw new TestFailure();
        protected override void Abstract(Message message) => throw new TestFailure();
    }

    private sealed class TestFailure : Exception { }

    private sealed class ThrowingOwner
    {
        public readonly TestFailure Failure = new();
        [EventHandler(EventPriority.High)] private void OnMessage(Message message) => throw Failure;
    }

    private sealed class StaticThrowingOwner
    {
        public static readonly TestFailure Failure = new();
        [EventHandler] private static void OnMessage(Message message) => throw Failure;
    }

    private sealed class EqualOwner : InstanceOwner
    {
        public override bool Equals(object? obj) => obj is EqualOwner;
        public override int GetHashCode() => 0;
    }

    private sealed class EqualListener : IListener
    {
        public Type Target => typeof(Message);
        public int Priority => EventPriority.Medium;
        public int Calls;
        public void Call(object value) => Calls++;
        public override bool Equals(object? obj) => obj is EqualListener;
        public override int GetHashCode() => 0;
    }

    private sealed class LambdaOwner
    {
        public Message? Received;
        public static Message? StaticReceived;
        [EventHandler(123)] private void Instance(Message message) => Received = message;
        [EventHandler(EventPriority.Low)] private static void Static(Message message) => StaticReceived = message;
    }
}
