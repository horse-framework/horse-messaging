namespace Horse.Messaging.Protocol
{
    /// <summary>
    /// Options for pending acknowledge or response from binding receiver
    /// </summary>
    public enum BindingInteraction
    {
        /// <summary>
        /// No response is pending
        /// </summary>
        None,

        /// <summary>
        /// Receiver should respond
        /// </summary>
        Response
    }
}