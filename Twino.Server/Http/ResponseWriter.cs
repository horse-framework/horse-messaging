using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.Http
{
    /// <summary>
    /// Writes plain HTTP Response data from the HttpResponse class
    /// </summary>
    internal class ResponseWriter
    {
        private TwinoServer _server;

        public ResponseWriter(TwinoServer server)
        {
            _server = server;
        }

        /// <summary>
        /// Writes plain HTTP Response data from the HttpResponse class
        /// </summary>
        internal async Task Write(HttpResponse response)
        {
            Stream stream = response.Stream;
            await using MemoryStream ms = new MemoryStream();
            byte[] result;
            byte[] content = response.GetContent();
            if (content != null && content.Length > 0)
            {
                switch (response.ContentEncoding)
                {
                    case ContentEncodings.Brotli:
                    {
                        await using (BrotliStream brotli = new BrotliStream(ms, CompressionMode.Compress))
                            await brotli.WriteAsync(content, 0, content.Length);

                        result = ms.ToArray();
                        break;
                    }
                    case ContentEncodings.Gzip:
                    {
                        await using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress))
                            await gzip.WriteAsync(content, 0, content.Length);

                        result = ms.ToArray();
                        break;
                    }
                    default:
                        result = content;
                        break;
                }
            }
            else
                result = new byte[0];

            StringBuilder responseBuilder = new StringBuilder();
            responseBuilder.Append(HttpHeaders.Create(HttpHeaders.HTTP_VERSION + " " + Convert.ToInt32(response.StatusCode) + " " + response.StatusCode));
            responseBuilder.Append(HttpHeaders.Create(HttpHeaders.SERVER, HttpHeaders.VALUE_SERVER));
            responseBuilder.Append(HttpHeaders.Create(HttpHeaders.DATE, _server.Time));
            responseBuilder.Append(HttpHeaders.Create(HttpHeaders.CONTENT_TYPE, response.ContentType, HttpHeaders.VALUE_CHARSET_UTF8));

            responseBuilder.Append(HttpHeaders.Create(HttpHeaders.CONNECTION,
                                                      _server.Options.HttpConnectionTimeMax > 0
                                                          ? HttpHeaders.VALUE_KEEP_ALIVE
                                                          : HttpHeaders.VALUE_CLOSE));

            switch (response.ContentEncoding)
            {
                case ContentEncodings.Brotli:
                    responseBuilder.Append(HttpHeaders.Create(HttpHeaders.CONTENT_ENCODING, HttpHeaders.VALUE_BROTLI));
                    break;
                case ContentEncodings.Gzip:
                    responseBuilder.Append(HttpHeaders.Create(HttpHeaders.CONTENT_ENCODING, HttpHeaders.VALUE_GZIP));
                    break;
            }

            responseBuilder.Append(HttpHeaders.Create(HttpHeaders.CONTENT_LENGTH, result.Length));

            foreach (var header in response.AdditionalHeaders)
                responseBuilder.Append(HttpHeaders.Create(header.Key, header.Value));

            responseBuilder.Append(Environment.NewLine);

            string str = responseBuilder.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(str);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.WriteAsync(result, 0, result.Length);
        }
    }
}