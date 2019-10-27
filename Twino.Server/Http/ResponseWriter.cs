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

        private static async Task Write(Stream stream, string msg)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            await stream.WriteAsync(data, 0, data.Length);
        }

        private static async Task Write(Stream stream, string key, string value)
        {
            byte[] kbytes = Encoding.ASCII.GetBytes(key);
            byte[] vbytes = Encoding.UTF8.GetBytes(value);
            await stream.WriteAsync(kbytes, 0, kbytes.Length);
            await stream.WriteAsync(new byte[] {HttpReader.COLON, HttpReader.SPACE}, 0, 2);
            await stream.WriteAsync(vbytes, 0, vbytes.Length);
            await stream.WriteAsync(HttpReader.CRLF, 0, 2);
        }

        /// <summary>
        /// Writes plain HTTP Response data from the HttpResponse class
        /// </summary>
        internal async Task Write(HttpResponse response)
        {
            Stream stream = response.Stream;
            byte[] result;
            byte[] content = response.GetContent();
            if (content != null && content.Length > 0)
            {
                switch (response.ContentEncoding)
                {
                    case ContentEncodings.Brotli:
                    {
                        await using MemoryStream ms = new MemoryStream();
                        await using (BrotliStream brotli = new BrotliStream(ms, CompressionMode.Compress))
                            await brotli.WriteAsync(content, 0, content.Length);

                        result = ms.ToArray();
                        break;
                    }
                    case ContentEncodings.Gzip:
                    {
                        await using MemoryStream ms = new MemoryStream();
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

            await using MemoryStream m = new MemoryStream();

            await Write(m, HttpHeaders.Create(HttpHeaders.HTTP_VERSION + " " + Convert.ToInt32(response.StatusCode) + " " + response.StatusCode));
            await Write(m, HttpHeaders.SERVER, HttpHeaders.VALUE_SERVER);
            await Write(m, _server.Time);
            await Write(m, HttpHeaders.CONTENT_TYPE, response.ContentType + ";" + HttpHeaders.VALUE_CHARSET_UTF8);

            await Write(m, HttpHeaders.CONNECTION,
                        _server.Options.HttpConnectionTimeMax > 0
                            ? HttpHeaders.VALUE_KEEP_ALIVE
                            : HttpHeaders.VALUE_CLOSE);

            switch (response.ContentEncoding)
            {
                case ContentEncodings.Brotli:
                    await Write(m, HttpHeaders.CONTENT_ENCODING, HttpHeaders.VALUE_BROTLI);
                    break;
                case ContentEncodings.Gzip:
                    await Write(m, HttpHeaders.CONTENT_ENCODING, HttpHeaders.VALUE_GZIP);
                    break;
            }

            await Write(m, HttpHeaders.CONTENT_LENGTH, result.Length.ToString());

            foreach (var header in response.AdditionalHeaders)
                await Write(m, header.Key, header.Value);

            await m.WriteAsync(HttpReader.CRLF, 0, 2);

            m.WriteTo(stream);
            await stream.WriteAsync(new ReadOnlyMemory<byte>(result));
        }
    }
}