using System.IO;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    public interface IProtocolMessageWriter<in TMessage>
    {
        Task Write(TMessage value, Stream stream);
        
        Task<byte[]> Create(TMessage value);

        Task<byte[]> CreateFrame(TMessage value);

        Task<byte[]> CreateContent(TMessage value);
    }
}