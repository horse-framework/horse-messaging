using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    /// <summary>
    /// Error Response for Twino Request Handler objects
    /// </summary>
    /// <typeparam name="TResponse">Response model of the handler</typeparam>
    public class ErrorResponse<TResponse>
    {
        /// <summary>
        /// Result code
        /// </summary>
        public TwinoResultCode ResultCode { get; set; }
        
        /// <summary>
        /// Error response model
        /// </summary>
        public TResponse ErrorModel { get; set; }

        /// <summary>
        /// Error Reason message
        /// </summary>
        public string Reason { get; set; }
    }
}