using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Twino.Core.Http;

namespace Twino.Server.Http
{
    public class ContentWriter
    {
        private readonly TwinoServer _server;

        public ContentWriter(TwinoServer server)
        {
            _server = server;
        }

        public async Task<Stream> WriteAsync(HttpRequest request, HttpResponse response)
        {
            if (!response.HasStream())
                return null;
            
            if (response.SuppressContentEncoding || _server.SupportedEncodings.Length == 0 || string.IsNullOrEmpty(request.AcceptEncoding))
            {
                response.ContentEncoding = ContentEncodings.None;
                return response.ResponseStream;
            }

            foreach (ContentEncodings encoding in _server.SupportedEncodings)
            {
                if (EncodingIsAccepted(request, encoding))
                    return await WriteEncoded(response, encoding);
            }

            response.ContentEncoding = ContentEncodings.None;
            return response.ResponseStream;
        }

        private bool EncodingIsAccepted(HttpRequest request, ContentEncodings encoding)
        {
            return encoding switch
            {
                ContentEncodings.Brotli => request.AcceptEncoding.Contains("br", StringComparison.InvariantCultureIgnoreCase),
                ContentEncodings.Gzip => request.AcceptEncoding.Contains("gzip", StringComparison.InvariantCultureIgnoreCase),
                ContentEncodings.Deflate => request.AcceptEncoding.Contains("deflate", StringComparison.InvariantCultureIgnoreCase),
                _ => false
            };
        }

        private async Task<Stream> WriteEncoded(HttpResponse response, ContentEncodings encoding)
        {
            response.ResponseStream.Position = 0;
            response.ContentEncoding = encoding;

            MemoryStream stream = new MemoryStream();
            await using MemoryStream ms = new MemoryStream();
            Stream encodingStream = CreateEncodingStream(ms, encoding);
            await response.ResponseStream.CopyToAsync(encodingStream);
            await encodingStream.FlushAsync();
            
            ms.Position = 0;
            await ms.CopyToAsync(stream);
            stream.Position = 0;

            await encodingStream.DisposeAsync();
            
            return stream;
        }

        private Stream CreateEncodingStream(Stream parent, ContentEncodings encoding)
        {
            return encoding switch
            {
                ContentEncodings.Brotli => (Stream) new BrotliStream(parent, CompressionMode.Compress),
                ContentEncodings.Deflate => new DeflateStream(parent, CompressionMode.Compress),
                ContentEncodings.Gzip => new GZipStream(parent, CompressionMode.Compress),
                _ => null
            };
        }
    }
}