using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Twino.Mvc.Controllers;
using Twino.Protocols.Http;
using Twino.Server.Http;

namespace Twino.Mvc.Results
{
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

        public FileResult(HttpStatusCode statusCode)
        {
            Code = statusCode;
            Headers = new Dictionary<string, string>();
        }
        
        public FileResult(Stream stream, string filename)
        {
            Code = HttpStatusCode.OK;
            Headers = new Dictionary<string, string>();
            Set(stream, MimeTypes.GetMimeType(filename), filename);
        }

        public FileResult(Stream stream, string filename, string contentType)
        {
            Code = HttpStatusCode.OK;
            Headers = new Dictionary<string, string>();
            Set(stream, contentType, filename);
        }

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