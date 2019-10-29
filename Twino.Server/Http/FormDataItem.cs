using System.IO;

namespace Twino.Server.Http
{
    public class FormDataItem
    {
        /// <summary>
        /// "form-data" or "file"
        /// </summary>
        public string Type { get; set; }

        public string Name { get; set; }
        public string Filename { get; set; }

        // "text/plain", "multipart/mixed", "image/gif", "application/octet-stream"
        public string ContentType { get; set; }
        public string Boundary { get; set; }

        public bool IsFile { get; set; }

        public string TransferEncoding { get; set; }
        
        public MemoryStream Stream { get; set; }
    }
}