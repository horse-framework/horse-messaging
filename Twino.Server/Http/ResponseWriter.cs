using System;
using System.ComponentModel;
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
        private readonly TwinoServer _server;

        public ResponseWriter(TwinoServer server)
        {
            _server = server;
        }

        private static async Task Write(Stream stream, string msg)
        {
            byte[] data = Encoding.ASCII.GetBytes(msg);
            await stream.WriteAsync(data, 0, data.Length);
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
            MemoryStream memoryStream;
            if (response.ResponseStream.Length > 0)
            {
                switch (response.ContentEncoding)
                {
                    case ContentEncodings.Brotli:
                    {
                        memoryStream = new MemoryStream();
                        await using BrotliStream brotli = new BrotliStream(memoryStream, CompressionMode.Compress);
                        response.ResponseStream.WriteTo(brotli);

                        break;
                    }
                    case ContentEncodings.Gzip:
                    {
                        memoryStream = new MemoryStream();
                        await using GZipStream gzip = new GZipStream(memoryStream, CompressionMode.Compress);
                        response.ResponseStream.WriteTo(gzip);
                        break;
                    }

                    default:
                        memoryStream = response.ResponseStream;
                        break;
                }
            }
            else
                memoryStream = response.ResponseStream;

            await using MemoryStream m = new MemoryStream();

            await Write(m, HttpHeaders.Create(HttpHeaders.HTTP_VERSION + " " + Convert.ToInt32(response.StatusCode) + " " + response.StatusCode));
            await Write(m, HttpHeaders.SERVER, HttpHeaders.VALUE_SERVER);
            await m.WriteAsync(TwinoServer.Time, 0, TwinoServer.Time.Length);

            if (!string.IsNullOrEmpty(response.ContentType))
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

            await Write(m, HttpHeaders.CONTENT_LENGTH, memoryStream.Length.ToString());

            foreach (var header in response.AdditionalHeaders)
                await Write(m, header.Key, header.Value);

            await m.WriteAsync(HttpReader.CRLF, 0, 2);
            memoryStream.WriteTo(m);

            m.WriteTo(stream);
            //memoryStream.WriteTo(stream);
        }
    }
}