using System.IO;

namespace Twino.Protocols.Http.Forms
{
    /// <summary>
    /// HTTP Posted form file
    /// </summary>
    public interface IFormFile
    {
        /// <summary>
        /// File size in bytes
        /// </summary>
        int Size { get; }

        /// <summary>
        /// File content type
        /// </summary>
        string ContentType { get; }

        /// <summary>
        /// File Form name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// File pyhsical filename
        /// </summary>
        string Filename { get; }

        /// <summary>
        /// File content stream
        /// </summary>
        Stream Stream { get; }
    }
}