namespace Horse.Mq.Queues
{
    /// <summary>
    /// Result sets of pull operations
    /// </summary>
    public enum PullResult
    {
        /// <summary>
        /// Message is pulled successfuly
        /// </summary>
        Success,

        /// <summary>
        /// Queue is empty
        /// </summary>
        Empty,

        /// <summary>
        /// Queue status does not support pulling messages 
        /// </summary>
        StatusNotSupported,
        
        /// <summary>
        /// Pull request is unacceptable
        /// </summary>
        Unacceptable
    }
}