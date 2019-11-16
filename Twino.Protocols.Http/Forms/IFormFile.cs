using System.IO;

namespace Twino.Protocols.Http
{
    public interface IFormFile
    {
        int Size { get; }
        string ContentType { get; }
        string Name { get; }
        string Filename { get; }
        Stream Stream { get; }
    }

    internal class FormFile : IFormFile
    {
        public int Size { get; internal set; }
        public string ContentType { get; internal set; }
        public string Name { get; internal set; }
        public string Filename { get; internal set; }
        public Stream Stream { get; internal set; }
    }
}