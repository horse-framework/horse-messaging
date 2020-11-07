using Twino.Protocols.TMQ;

namespace Twino.MQ.Client
{
    /// <summary>
    /// Error Response for Twino Request Handler objects
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Result code
        /// </summary>
        public TwinoResultCode ResultCode { get; set; }

        /// <summary>
        /// Error Reason message
        /// </summary>
        public string Reason { get; set; }
    }
}