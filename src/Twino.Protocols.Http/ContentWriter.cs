using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Twino.Protocols.Http
{
    /// <summary>
    /// Response content writer class.
    /// Finds encoder and writes response content to a stream
    /// </summary>
    public class ContentWriter
    {
        /// <summary>
        /// Supported encoding for the content write operation
        /// </summary>
        private readonly ContentEncodings[] _supportedEncodings;

        public ContentWriter(ContentEncodings[] supportedEncodings)
        {
            _supportedEncodings = supportedEncodings;
        }

        /// <summary>
        /// Writes response content to a stream and returns it
        /// </summary>
        public async Task<Stream> WriteAsync(HttpRequest request, HttpResponse response)
        {
            if (string.IsNullOrEmpty(request.AcceptEncoding))
            {
                response.ContentEncoding = ContentEncodings.None;
                return response.ResponseStream;
            }

            foreach (ContentEncodings encoding in _supportedEncodings)
            {
                if (EncodingIsAccepted(request, encoding))
                    return await WriteEncoded(response, encoding);
            }

            response.ContentEncoding = ContentEncodings.None;
            return response.ResponseStream;
        }

        /// <summary>
        /// Returns true if encoding is supported by client
        /// </summary>
        private static bool EncodingIsAccepted(HttpRequest request, ContentEncodings encoding)
        {
            switch (encoding)
            {
                case ContentEncodings.Brotli: return request.AcceptEncoding.Contains("br", StringComparison.InvariantCultureIgnoreCase);
                case ContentEncodings.Gzip: return request.AcceptEncoding.Contains("gzip", StringComparison.InvariantCultureIgnoreCase);
                case ContentEncodings.Deflate: return request.AcceptEncoding.Contains("deflate", StringComparison.InvariantCultureIgnoreCase);
                default: return false;
            }
        }

        /// <summary>
        /// Writes response content to stream with specified encoding
        /// </summary>
        private async Task<Stream> WriteEncoded(HttpResponse response, ContentEncodings encoding)
        {
            response.ResponseStream.Position = 0;
            response.ContentEncoding = encoding;

            MemoryStream ms = new MemoryStream();
            Stream encodingStream = CreateEncodingStream(ms, encoding);
            await response.ResponseStream.CopyToAsync(encodingStream);
            await encodingStream.FlushAsync();

            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Creates encoding stream from encoding enum value
        /// </summary>
        private Stream CreateEncodingStream(Stream parent, ContentEncodings encoding)
        {
            switch (encoding)
            {
                case ContentEncodings.Brotli: return new BrotliStream(parent, CompressionMode.Compress);
                case ContentEncodings.Gzip: return new DeflateStream(parent, CompressionMode.Compress);
                case ContentEncodings.Deflate: return new GZipStream(parent, CompressionMode.Compress);
                default: return null;
            }
        }
    }
}