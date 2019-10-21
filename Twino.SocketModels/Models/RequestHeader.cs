using Newtonsoft.Json;

namespace Twino.SocketModels.Models
{
    /// <summary>
    /// The header information for Request Manager requests
    /// </summary>
    public class RequestHeader
    {
        /// <summary>
        /// Request's unique id, will be used for response
        /// </summary>
        [JsonProperty("unique")]
        public string Unique { get; set; }
        
        /// <summary>
        /// Request type code
        /// </summary>
        [JsonProperty("requestType")]
        public int RequestType { get; set; }
        
        /// <summary>
        /// Expected response type code
        /// </summary>
        [JsonProperty("responseType")]
        public int ResponseType { get; set; }
    }
}