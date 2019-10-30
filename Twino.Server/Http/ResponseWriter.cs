using System;
using System.IO;
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
        private readonly TwinoServer _server;
        private readonly ContentWriter _writer;

        public ResponseWriter(TwinoServer server)
        {
            _server = server;
            _writer = new ContentWriter(server);
        }

        private static async Task Write(Stream stream, string msg)
        {
            ReadOnlyMemory<byte> data = Encoding.ASCII.GetBytes(msg);
            await stream.WriteAsync(data);
        }

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

            if (response.ResponseStream.Length > 0 && _server.SupportedEncodings.Length > 0)
                resultStream = await _writer.WriteAsync(response.Request, response);
            else
                resultStream = response.ResponseStream;

            await using MemoryStream m = new MemoryStream();

            await m.WriteAsync(PredefinedHeaders.HTTP_VERSION);
            await Write(m, HttpHeaders.Create(Convert.ToInt32(response.StatusCode) + " " + response.StatusCode));
            await m.WriteAsync(PredefinedHeaders.SERVER_CRLF);
            await m.WriteAsync(PredefinedHeaders.SERVER_TIME_CRLF);

            if (!string.IsNullOrEmpty(response.ContentType))
            {
                await m.WriteAsync(PredefinedHeaders.CONTENT_TYPE_COLON);
                await Write(m, response.ContentType);
                await m.WriteAsync(PredefinedHeaders.CHARSET_UTF8_CRLF);
            }

            if (_server.Options.HttpConnectionTimeMax > 0)
                await m.WriteAsync(PredefinedHeaders.CONNECTION_KEEP_ALIVE_CRLF);
            else
                await m.WriteAsync(PredefinedHeaders.CONNECTION_CLOSE_CRLF);

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

            if (resultStream.Length > 0)
            {
                await m.WriteAsync(PredefinedHeaders.CONTENT_LENGTH_COLON);
                await Write(m, resultStream.Length + "\r\n");
            }

            foreach (var header in response.AdditionalHeaders)
                await Write(m, header.Key, header.Value);

            await m.WriteAsync(HttpReader.CRLF, 0, 2);

            resultStream.Position = 0;
            await resultStream.CopyToAsync(m);

            m.WriteTo(stream);

            if (response.StreamSuppressed && response.ResponseStream != null)
                GC.ReRegisterForFinalize(response.ResponseStream);
        }
    }
}