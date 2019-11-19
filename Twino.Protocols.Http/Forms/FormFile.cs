using System.IO;

namespace Twino.Protocols.Http.Forms
{
    /// <summary>
    /// HTTP Posted form file
    /// </summary>
    internal class FormFile : IFormFile
    {
        /// <summary>
        /// File size in bytes
        /// </summary>
        public int Size { get; internal set; }

        /// <summary>
        /// File content type
        /// </summary>
        public string ContentType { get; internal set; }

        /// <summary>
        /// File Form name
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// File pyhsical filename
        /// </summary>
        public string Filename { get; internal set; }

        /// <summary>
        /// File content stream
        /// </summary>
        public Stream Stream { get; internal set; }
    }
}