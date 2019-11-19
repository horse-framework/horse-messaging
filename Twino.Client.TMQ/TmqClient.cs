using System.Threading.Tasks;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    public class TmqClient : ClientSocketBase<TmqMessage>
    {
        public override void Connect(string host)
        {
            throw new System.NotImplementedException();
        }

        protected override Task Read()
        {
            throw new System.NotImplementedException();
        }

        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }
    }
}