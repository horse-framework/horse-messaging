namespace Horse.Messaging.Client.Direct.Annotations
{
    /// <summary>
    /// Error response types.
    /// Descibes what find of error reason will be sent to server when an exception is thrown while processing the message
    /// </summary>
    public enum ErrorResponseType
    {
        /// <summary>
        /// Reason none
        /// </summary>
        None,

        /// <summary>
        /// Reason error
        /// </summary>
        Error,

        /// <summary>
        /// Reason is class name of the exception
        /// </summary>
        ExceptionType,

        /// <summary>
        /// Reason is messge of the exception
        /// </summary>
        ExceptionMessage
    }
}