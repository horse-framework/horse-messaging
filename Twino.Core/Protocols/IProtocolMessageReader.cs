using System.IO;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    public interface IProtocolMessageReader<TMessage>
    {
        Task<TMessage> Read(Stream stream);
    }
}