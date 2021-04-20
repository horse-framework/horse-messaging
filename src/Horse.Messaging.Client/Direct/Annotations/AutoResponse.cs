namespace Horse.Messaging.Client.Direct.Annotations
{
    /// <summary>
    /// Auto response type.
    /// Describes when response message will be sent to server.
    /// </summary>
    public enum AutoResponse
    {
        /// <summary>
        /// Only success operations sends successful response
        /// </summary>
        OnSuccess,

        /// <summary>
        /// Only failed operations sends failed response
        /// </summary>
        OnError,

        /// <summary>
        /// Both success and error operations sends response
        /// </summary>
        All
    }
}