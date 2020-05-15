namespace Twino.Protocols.TMQ
{
    /// <summary>
    /// TmqClient and TmqAdminClient process result enum
    /// </summary>
    public enum TwinoResult : ushort
    {
        /// <summary>
        /// Operation succeeded
        /// </summary>
        Ok = 0,

        /// <summary>
        /// Unknown failed response
        /// </summary>
        Failed = 1,
        
        /// <summary>
        /// Request is not recognized or verified by the server
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// Access denied for the operation
        /// </summary>
        Unauthorized = 401,

        /// <summary>
        /// Target could not be found
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// Request is not acceptable. Eg, queue status does not support the operation
        /// </summary>
        Unacceptable = 406,

        /// <summary>
        /// Requested data is already exists
        /// </summary>
        Duplicate = 481,

        /// <summary>
        /// Client, channel, consumer, queue or message limit is exceeded
        /// </summary>
        LimitExceeded = 482,

        /// <summary>
        /// Target is busy to complete the process
        /// </summary>
        Busy = 503,

        /// <summary>
        /// Message could not be sent to the server
        /// </summary>
        SendError = 581
    }
    
}