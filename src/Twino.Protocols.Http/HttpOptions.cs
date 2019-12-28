using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

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
        public int HttpConnectionTimeMax { get; set; } = 300;

        /// <summary>
        /// Maximum request lengths (includes content)
        /// </summary>
        public int MaximumRequestLength { get; set; } = 1024 * 100;

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
                       MaximumRequestLength = 1024 * 100
                   };
        }

        /// <summary>
        /// Loads options from filename
        /// </summary>
        public static HttpOptions Load(string filename)
        {
            string json = System.IO.File.ReadAllText(filename);
            JObject obj = JObject.Parse(json);

            HttpOptions options = CreateDefault();

            JToken timeMax = obj["HttpConnectionTimeMax"];
            if (timeMax != null)
                options.HttpConnectionTimeMax = timeMax.Value<int>();

            JToken requestLength = obj["MaximumRequestLength"];
            if (requestLength != null)
                options.MaximumRequestLength = requestLength.Value<int>();

            JToken hostnames = obj["Hostnames"];
            if (hostnames != null && hostnames.HasValues)
                options.Hostnames = obj["Hostnames"].Values<string>().ToArray();

            JToken supportedEncodings = obj["SupportedEncodings"];
            string[] sx = supportedEncodings != null ? supportedEncodings.Values<string>().ToArray() : new string[0];
            List<ContentEncodings> encodings = new List<ContentEncodings>();

            foreach (string s in sx)
            {
                if (string.IsNullOrWhiteSpace(s))
                    continue;

                switch (s.Trim().ToLower())
                {
                    case "none":
                        encodings.Add(ContentEncodings.None);
                        break;
                    case "gzip":
                        encodings.Add(ContentEncodings.Gzip);
                        break;
                    case "br":
                        encodings.Add(ContentEncodings.Brotli);
                        break;
                    case "brotli":
                        encodings.Add(ContentEncodings.Brotli);
                        break;
                    case "deflate":
                        encodings.Add(ContentEncodings.Deflate);
                        break;
                }
            }

            if (encodings.Count == 0)
                encodings.Add(ContentEncodings.None);

            options.SupportedEncodings = encodings.ToArray();

            return options;
        }
    }
}