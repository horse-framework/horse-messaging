using System;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Protocols.TMQ;

namespace Twino.Client.TMQ
{
    public class TmqClient : ClientSocketBase<TmqMessage>
    {
        #region Connect - Read

        public override void Connect(string host)
        {
            throw new System.NotImplementedException();
        }

        protected override Task Read()
        {
            throw new System.NotImplementedException();
        }

        #endregion

        #region Ping - Pong

        public override void Ping()
        {
            Send(PredefinedMessages.PING);
        }

        public override void Pong()
        {
            Send(PredefinedMessages.PONG);
        }

        #endregion

        #region Send

        public bool Send(TmqMessage message)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> SendAsync(TmqMessage message)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}