namespace Twino.Protocols.Http
{
    public class HttpOptions
    {
        public int HttpConnectionTimeMax { get; set; }
        public int MaximumRequestLength { get; set; }
        public ContentEncodings[] SupportedEncodings { get; set; }

        public string[] Hostnames { get; set; }

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