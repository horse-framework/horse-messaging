using System;
using System.Text;
using System.Threading.Tasks;

namespace Twino.Core.WebSocket
{
    public abstract class TwinoWebSocket : SocketBase
    {
        /// <summary>
        /// Read buffer 512 bytes
        /// </summary>
        private readonly byte[] _buffer = new byte[512];

        /// <summary>
        /// Reader class for last receiving network package
        /// </summary>
        private WebSocketReader _reader;

        protected TwinoWebSocket()
        {
            _reader = new WebSocketReader();
        }

        /// <summary>
        /// Starts to read from the TCP socket
        /// </summary>
        protected override async Task Read()
        {
            if (Stream == null)
            {
                Disconnect();
                return;
            }

            int read = await Stream.ReadAsync(_buffer, 0, _buffer.Length);
            if (read < 1)
            {
                Disconnect();
                return;
            }

            int offset = 0;
            while (offset < read)
            {
                //reads to the end of the package
                //package does not mean received data
                //package is the websocket protocol package.
                //there can be multiple packages on network stream waiting for receiving
                //or package may be larger than received
                offset += _reader.Read(_buffer, offset, read);

                //if reader is ready, we have ready websocket package, just deliver
                if (_reader.IsReady)
                {
                    switch (_reader.OpCode)
                    {
                        //PING received, send PONG asap
                        case SocketOpCode.Ping:
                            byte[] pong = WebSocketWriter.CreatePong();
                            Send(pong);
                            break;

                        case SocketOpCode.Pong:
                            PongTime = DateTime.UtcNow;
                            break;

                        //UTF8 OP Code text message received
                        case SocketOpCode.UTF8:
                            string result = Encoding.UTF8.GetString(_reader.Payload);
                            OnMessageReceived(result);
                            break;

                        //Binary OP Code binary received
                        case SocketOpCode.Binary:
                            byte[] payload = _reader.Payload;
                            OnBinaryReceived(payload);
                            break;

                        //Terminate OP code tells us to disconnect
                        case SocketOpCode.Terminate:
                            Disconnect();
                            return;

                        default:
                            Disconnect();
                            return;
                    }

                    _reader = new WebSocketReader();
                }
            }
        }

        /// <summary>
        /// Sends a string message to the socket client.
        /// Data must be plain text,
        /// WebSocket protocol information will be added
        /// </summary>
        public override bool Send(string message)
        {
            try
            {
                byte[] data = WebSocketWriter.CreateFromUTF8(message);
                return Send(data);
            }
            catch (Exception ex)
            {
                OnError("SEND_STRING", ex);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Sends a string message to the socket client.
        /// Data must be plain text,
        /// WebSocket protocol information will be added
        /// </summary>
        public override async Task<bool> SendAsync(string message)
        {
            try
            {
                byte[] data = await WebSocketWriter.CreateFromUTF8Async(message);
                return Send(data);
            }
            catch (Exception ex)
            {
                OnError("SEND_STRING", ex);
                Disconnect();
                return false;
            }
        }
    }
}