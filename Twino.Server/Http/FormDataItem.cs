using System.IO;

namespace Twino.Server.Http
{
    /// <summary>
    /// multipart/form-data item
    /// </summary>
    public class FormDataItem
    {
        /// <summary>
        /// "form-data" or "file"
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// form-data name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// If item is file, filename. otherwise null
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        /// Content type such as "text/plain", "multipart/mixed", "image/gif", "application/octet-stream"
        /// </summary>
        public string ContentType { get; set; }
        
        /// <summary>
        /// If item is mixed, item's boundary value. otherwise null
        /// </summary>
        public string Boundary { get; set; }

        /// <summary>
        /// True, if item is file
        /// </summary>
        public bool IsFile { get; set; }

        /// <summary>
        /// Item content as stream
        /// </summary>
        public MemoryStream Stream { get; set; }
    }
}