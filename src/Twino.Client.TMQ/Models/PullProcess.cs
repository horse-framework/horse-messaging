namespace Twino.Client.TMQ.Models
{
    /// <summary>
    /// Pull process status
    /// </summary>
    public enum PullProcess
    {
        /// <summary>
        /// Still receiving messages from server
        /// </summary>
        Receiving,

        /// <summary>
        /// Server respond unacceptable request
        /// </summary>
        Unacceptable,

        /// <summary>
        /// Unauthorized process
        /// </summary>
        Unauthorized,

        /// <summary>
        /// Queue is empty
        /// </summary>
        Empty,

        /// <summary>
        /// All messages are received, process completed.
        /// </summary>
        Completed,

        /// <summary>
        /// Message receive operation timed out.
        /// </summary>
        Timeout,

        /// <summary>
        /// Disconnected from server while receiving messages
        /// </summary>
        NetworkError
    }
}