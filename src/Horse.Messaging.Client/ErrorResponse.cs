using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client
{
    /// <summary>
    /// Error Response for Horse Request Handler objects
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Result code
        /// </summary>
        public HorseResultCode ResultCode { get; set; }

        /// <summary>
        /// Error Reason message
        /// </summary>
        public string Reason { get; set; }
    }
}