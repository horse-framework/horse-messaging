namespace Twino.Protocols.Http
{
    /// <summary>
    /// HTTP Protocol option for Twino HTTP Server
    /// </summary>
    public class HttpOptions
    {
        /// <summary>
        /// Maximum keeping alive duration for each TCP connection
        /// </summary>
        public int HttpConnectionTimeMax { get; set; }
        
        /// <summary>
        /// Maximum request lengths (includes content)
        /// </summary>
        public int MaximumRequestLength { get; set; }
        
        /// <summary>
        /// Supported encodings (Only used when clients accept)
        /// </summary>
        public ContentEncodings[] SupportedEncodings { get; set; }

        /// <summary>
        /// Listening hostnames.
        /// In order to accept all hostnames skip null or set 1-length array with "*" element 
        /// </summary>
        public string[] Hostnames { get; set; }

        /// <summary>
        /// Createsd default HTTP server options
        /// </summary>
        public static HttpOptions CreateDefault()
        {
            return new HttpOptions
                   {
                       HttpConnectionTimeMax = 300,
                       MaximumRequestLength = 1024 * 100,
                       SupportedEncodings = new[]
                                            {
                                                ContentEncodings.Brotli,
                                                ContentEncodings.Gzip
                                            }
                   };
        }
        
    }
}