namespace CSorbit
{
    public enum ListenerInvocationMode
    {
        /// <summary>Bind typed delegates once when subscribing; fast event delivery.</summary>
        Delegate,

        /// <summary>Invoke attributed methods through reflection for AOT runtimes.</summary>
        Reflection
    }
}
