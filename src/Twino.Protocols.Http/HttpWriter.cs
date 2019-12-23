using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Twino.Core.Protocols;
using Twino.Server.Http;

namespace Twino.Protocols.Http
{
    /// <summary>
    /// Response of writing HTTP Responses to network streams
    /// </summary>
    public class HttpWriter
    {
        /// <summary>
        /// Content writer for HTTP response
        /// </summary>
        private readonly ContentWriter _writer;

        /// <summary>
        /// Twino HTTP Server options
        /// </summary>
        private readonly HttpOptions _options;

        public HttpWriter(HttpOptions options)
        {
            _options = options;
            _writer = new ContentWriter(_options.SupportedEncodings);
        }

        /// <summary>
        /// Writes http message to specified stream
        /// </summary>
        public async Task Write(HttpMessage value, Stream stream)
        {
            await Write(value.Response);
        }

        /// <summary>
        /// Writes string value to specified stream
        /// </summary>
        private static async Task Write(Stream stream, string msg)
        {
            ReadOnlyMemory<byte> data = Encoding.ASCII.GetBytes(msg);
            await stream.WriteAsync(data);
        }

        /// <summary>
        /// Writes header key and value to specified stream
        /// </summary>
        private static async Task Write(Stream stream, string key, string value)
        {
            byte[] data = Encoding.UTF8.GetBytes(key + ": " + value + "\r\n");
            await stream.WriteAsync(data, 0, data.Length);
        }

        /// <summary>
        /// Writes plain HTTP Response data from the HttpResponse class
        /// </summary>
        internal async Task Write(HttpResponse response)
        {
            Stream stream = response.NetworkStream;
            Stream resultStream;
            bool hasStream = response.HasStream() && response.ResponseStream.Length > 0;

            if (hasStream)
            {
                if (!response.SuppressContentEncoding && _options.SupportedEncodings.Length > 0)
                    resultStream = await _writer.WriteAsync(response.Request, response);
                else
                {
                    if (response.ContentEncoding != ContentEncodings.None)
                        response.ContentEncoding = ContentEncodings.None;

                    resultStream = response.ResponseStream;
                }
            }

            //if response stream does not exists
            //we are checking to find a response message for http status code
            else
            {
                byte[] bytes;

                bool found = PredefinedResults.Statuses.TryGetValue(response.StatusCode, out bytes);
                if (found && bytes != null)
                {
                    resultStream = new MemoryStream(bytes);
                    response.ContentEncoding = ContentEncodings.None;
                    hasStream = true;
                }
                else
                    resultStream = null;
            }

            await using MemoryStream m = new MemoryStream();

            //http version, http status, server info and server time
            await m.WriteAsync(PredefinedHeaders.HTTP_VERSION);
            await Write(m, HttpHeaders.Create(Convert.ToInt32(response.StatusCode) + " " + response.StatusCode));
            await m.WriteAsync(PredefinedHeaders.SERVER_CRLF);
            await m.WriteAsync(PredefinedHeaders.SERVER_TIME_CRLF);

            if (hasStream)
            {
                //content type
                if (!string.IsNullOrEmpty(response.ContentType))
                {
                    await m.WriteAsync(PredefinedHeaders.CONTENT_TYPE_COLON);
                    await Write(m, response.ContentType);
                    await m.WriteAsync(PredefinedHeaders.CHARSET_UTF8_CRLF);
                }

                //connection keep alive or close
                if (_options.HttpConnectionTimeMax > 0)
                    await m.WriteAsync(PredefinedHeaders.CONNECTION_KEEP_ALIVE_CRLF);
                else
                    await m.WriteAsync(PredefinedHeaders.CONNECTION_CLOSE_CRLF);

                //content encoding
                switch (response.ContentEncoding)
                {
                    case ContentEncodings.Brotli:
                        await m.WriteAsync(PredefinedHeaders.ENCODING_BR_CRLF);
                        break;

                    case ContentEncodings.Gzip:
                        await m.WriteAsync(PredefinedHeaders.ENCODING_GZIP_CRLF);
                        break;

                    case ContentEncodings.Deflate:
                        await m.WriteAsync(PredefinedHeaders.ENCODING_DEFLATE_CRLF);
                        break;
                }

                //content length
                await m.WriteAsync(PredefinedHeaders.CONTENT_LENGTH_COLON);
                await Write(m, resultStream.Length + "\r\n");
            }
            else
                await m.WriteAsync(PredefinedHeaders.CONNECTION_CLOSE_CRLF);

            //custom headers 
            foreach (var header in response.AdditionalHeaders)
                await Write(m, header.Key, header.Value);

            await m.WriteAsync(PredefinedMessages.CRLF, 0, 2);

            if (hasStream)
            {
                resultStream.Position = 0;

                //if data is greater than 5KB, don't waste memory and send it to network as partial
                if (resultStream.Length > 5120)
                {
                    m.WriteTo(stream);
                    await resultStream.CopyToAsync(stream);
                }

                //write small data to memory stream and send all data to network at once for better response time
                else
                {
                    await resultStream.CopyToAsync(m);
                    m.WriteTo(stream);
                }
            }
            else
                m.WriteTo(stream);

            //let gc to dispose response stream
            if (hasStream && response.StreamSuppressed && response.ResponseStream != null)
                GC.ReRegisterForFinalize(response.ResponseStream);
        }
    }
}