namespace Twino.Protocols.Http
{
    public class HttpOptions
    {
        public int HttpConnectionTimeMax { get; set; }
        public int MaximumRequestLength { get; set; }
        public ContentEncodings[] SupportedEncodings { get; set; }
        
        public string[] Hostnames { get; set; }
    }
}