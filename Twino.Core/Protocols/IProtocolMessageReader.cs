using System.IO;
using System.Threading.Tasks;

namespace Twino.Core.Protocols
{
    public interface IProtocolMessageReader<TMessage>
    {
        ProtocolHandshakeResult HandshakeResult { get; set; }
        
        Task<TMessage> Read(Stream stream);

        void Reset();
    }
}