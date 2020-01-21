namespace Twino.MQ.Queues
{
    /// <summary>
    /// Result sets of push operations
    /// </summary>
    public enum PushResult
    {
        /// <summary>
        /// Message is pushed successfuly
        /// </summary>
        Success,

        /// <summary>
        /// Message limit is exceeded in queue, push failed
        /// </summary>
        LimitExceeded,

        /// <summary>
        /// Queue status does not support pushing messages 
        /// </summary>
        StatusNotSupported
    }
}