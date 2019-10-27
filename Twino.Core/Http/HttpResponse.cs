using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Twino.Core.Http
{
    public enum ContentEncodings
    {
        None,
        Gzip,
        Brotli
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
        public Stream Stream { get; set; }

        /// <summary>
        /// Additional headers for the response.
        /// </summary>
        public Dictionary<string, string> AdditionalHeaders { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response content. The response byte array is created just be sending the data to the client.
        /// Until this operation, data wil be appended to the content string builder.
        /// </summary>
        private StringBuilder Content { get; } = new StringBuilder();

        #endregion

        /// <summary>
        /// Writes a string to the response
        /// </summary>
        public void Write(string content)
        {
            Content.Append(content);
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
            Content.Append(JsonConvert.SerializeObject(model));
        }

        /// <summary>
        /// Reads the content and creates byte array for shipping
        /// </summary>
        /// <returns></returns>
        public byte[] GetContent()
        {
            return Encoding.UTF8.GetBytes(Content.ToString());
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
                       ContentEncoding = ContentEncodings.None
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
                       ContentEncoding = ContentEncodings.None
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
                       ContentEncoding = ContentEncodings.None
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
                       ContentEncoding = ContentEncodings.None
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
                       ContentEncoding = ContentEncodings.None
                   };
        }
    }
}