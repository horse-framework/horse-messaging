namespace Horse.Mq.Routing
{
    /// <summary>
    /// Result sets of the router publish operation
    /// </summary>
    public enum RouterPublishResult
    {
        /// <summary>
        /// Router is disabled
        /// </summary>
        Disabled,

        /// <summary>
        /// There is no binding for the router
        /// </summary>
        NoBindings,

        /// <summary>
        /// There are bindings but they are not available
        /// </summary>
        NoReceivers,

        /// <summary>
        /// Message is sent to bindings and at least one of bindings will sent acknowledge or response
        /// </summary>
        OkAndWillBeRespond,

        /// <summary>
        /// Message is sent to bindings but none of them will response
        /// </summary>
        OkWillNotRespond
    }
}