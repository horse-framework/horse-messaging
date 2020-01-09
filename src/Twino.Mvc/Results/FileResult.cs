using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Twino.Protocols.Http;
using Twino.Server.Http;

namespace Twino.Mvc.Results
{
    /// <summary>
    /// File result for twino MVC
    /// </summary>
    public class FileResult : IActionResult
    {
        /// <summary>
        /// Result HTTP Status code
        /// </summary>
        public HttpStatusCode Code { get; set; }

        /// <summary>
        /// Result content type
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Result data stream
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// Additional custom headers with key and value
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        /// <summary>
        /// Creates new HTTP Status code file result
        /// </summary>
        public FileResult(HttpStatusCode statusCode)
        {
            Code = statusCode;
            Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// Creates new file result and finds MIME type from known mime types in twino
        /// </summary>
        public FileResult(Stream stream, string filename)
        {
            Code = HttpStatusCode.OK;
            Headers = new Dictionary<string, string>();
            Set(stream, MimeTypes.GetMimeType(filename), filename);
        }

        /// <summary>
        /// Creates new file result
        /// </summary>
        public FileResult(Stream stream, string filename, string contentType)
        {
            Code = HttpStatusCode.OK;
            Headers = new Dictionary<string, string>();
            Set(stream, contentType, filename);
        }

        /// <summary>
        /// Changes content of file result with new stream, conten type and filename
        /// </summary>
        private void Set(Stream stream, string contentType, string filename)
        {
            ContentType = contentType;
            Stream = stream;

            if (!string.IsNullOrEmpty(filename))
            {
                string ascii = Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(filename)).Replace("?", "_");
                string encoded = WebUtility.UrlEncode(filename);
                Headers.Add(HttpHeaders.CONTENT_DISPOSITION, $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{encoded}");
            }
        }
    }
}