namespace Twino.MQ.Routing
{
    /// <summary>
    /// Describes how messages are routed
    /// </summary>
    public enum RouteMethod
    {
        /// <summary>
        /// Routes each message to all bindings
        /// </summary>
        Distribute,

        /// <summary>
        /// Routes each message to only one binding
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Routes message to only first binding
        /// </summary>
        OnlyFirst
    }
}