using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Twino.Core.Http
{
    public enum ContentEncodings
    {
        None,
        Gzip,
        Brotli,
        Deflate
    }

    /// <summary>
    /// HttpResponse for HttpServer
    /// Used for only as response of Non-WebSocket HTTP Requests
    /// </summary>
    public class HttpResponse
    {
        #region Properties

        /// <summary>
        /// Status Code
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Content type such as (text/plain, application/json) can include charset information with ";" seperator
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Content encoding
        /// </summary>
        public ContentEncodings ContentEncoding { get; set; }

        /// <summary>
        /// Network stream of the Requester (if connection is using SSL, this stream is SslStream. otherwise NetworkStream)
        /// </summary>
        internal Stream NetworkStream { get; set; }

        /// <summary>
        /// Additional headers for the response.
        /// </summary>
        public Dictionary<string, string> AdditionalHeaders { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response content. The response byte array is created just be sending the data to the client.
        /// Until this operation, data wil be appended to the content string builder.
        /// </summary>
        // private StringBuilder Content { get; } = new StringBuilder();

        public MemoryStream ResponseStream { get; } = new MemoryStream();
        
        /// <summary>
        /// If true, prevents to apply content encodings. even server supports and client accepts.
        /// </summary>
        public bool SuppressContentEncoding { get; set; }
        
        /// <summary>
        /// Request of the response
        /// </summary>
        public HttpRequest Request { get; internal set; }
        
        #endregion

        /// <summary>
        /// Writes a string to the response
        /// </summary>
        public void Write(string content)
        {
            byte[] data = Encoding.UTF8.GetBytes(content);
            ResponseStream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Writes a string to the response
        /// </summary>
        public async Task WriteAsync(string content)
        {
            byte[] data = Encoding.UTF8.GetBytes(content);
            await ResponseStream.WriteAsync(data, 0, data.Length);
        }

        /// <summary>
        /// Writes a stream to the response stream
        /// </summary>
        public void Write(Stream stream)
        {
            stream.Position = 0;
            stream.CopyTo(ResponseStream);
        }

        /// <summary>
        /// Writes a stream to the response stream
        /// </summary>
        public async Task WriteAsync(Stream stream)
        {
            stream.Position = 0;
            await stream.CopyToAsync(ResponseStream);
        }

        /// <summary>
        /// Writes a string to the response
        /// </summary>
        public async Task WriteAsync(byte[] data)
        {
            await ResponseStream.WriteAsync(data, 0, data.Length);
        }

        /// <summary>
        /// Sets response content type to html and status to 200
        /// </summary>
        public void SetToText()
        {
            ContentType = ContentTypes.PLAIN_TEXT;
            StatusCode = HttpStatusCode.OK;
        }

        /// <summary>
        /// Sets response content type to html and status to 200
        /// </summary>
        public void SetToHtml()
        {
            ContentType = ContentTypes.TEXT_HTML;
            StatusCode = HttpStatusCode.OK;
        }

        /// <summary>
        /// Sets response content type to json and status to 200
        /// </summary>
        public void SetToJson(object model)
        {
            ContentType = ContentTypes.APPLICATION_JSON;
            StatusCode = HttpStatusCode.OK;
            byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model));
            ResponseStream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Sets response content type to json and status to 200
        /// </summary>
        public async Task SetToJsonAsync(object model)
        {
            ContentType = ContentTypes.APPLICATION_JSON;
            StatusCode = HttpStatusCode.OK;
            await System.Text.Json.JsonSerializer.SerializeAsync(ResponseStream, model, model.GetType());
        }

        /// <summary>
        /// 400 - Bad Request
        /// </summary>
        public static HttpResponse BadRequest()
        {
            return new HttpResponse
                   {
                       ContentType = ContentTypes.TEXT_HTML,
                       StatusCode = HttpStatusCode.BadRequest,
                       SuppressContentEncoding = true
                   };
        }

        /// <summary>
        /// 411 - Length Required
        /// </summary>
        public static HttpResponse LengthRequired()
        {
            return new HttpResponse
                   {
                       ContentType = ContentTypes.TEXT_HTML,
                       StatusCode = HttpStatusCode.LengthRequired,
                       SuppressContentEncoding = true
                   };
        }

        /// <summary>
        /// 201 - Created
        /// </summary>
        public static HttpResponse RequestUriTooLong()
        {
            return new HttpResponse
                   {
                       ContentType = ContentTypes.TEXT_HTML,
                       StatusCode = HttpStatusCode.RequestUriTooLong,
                       SuppressContentEncoding = true
                   };
        }

        /// <summary>
        /// 429 - Too Many Requests
        /// </summary>
        public static HttpResponse TooManyRequests()
        {
            return new HttpResponse
                   {
                       ContentType = ContentTypes.TEXT_HTML,
                       StatusCode = HttpStatusCode.TooManyRequests,
                       SuppressContentEncoding = true
                   };
        }

        /// <summary>
        /// 431 - Request Header Fields Too Large
        /// </summary>
        public static HttpResponse RequestHeaderFieldsTooLarge()
        {
            return new HttpResponse
                   {
                       ContentType = ContentTypes.TEXT_HTML,
                       StatusCode = HttpStatusCode.RequestHeaderFieldsTooLarge,
                       SuppressContentEncoding = true
                   };
        }
    }
}