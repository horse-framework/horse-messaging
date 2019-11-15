using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Twino.Core
{
    /// <summary>
    /// Function definition for string message receiving events of web sockets
    /// </summary>
    public delegate void SocketMessageHandler(SocketBase client, string message);

    /// <summary>
    /// Function definition for binary message receiving events of web sockets
    /// </summary>
    public delegate void SocketBinaryHandler(SocketBase client, byte[] payload);

    /// <summary>
    /// Base class for web socket clients.
    /// Server-side client and Client-side client classes are derived from this class
    /// </summary>
    public abstract class SocketBase
    {
        #region Properties

        public event SocketMessageHandler MessageReceived;
        public event SocketBinaryHandler BinaryReceived;

        /// <summary>
        /// TcpClient class of the socket class
        /// </summary>
        protected TcpClient Client { get; set; }

        /// <summary>
        /// TcpClient network stream (with SSL or without SSL, depends on the requested URL or server certificate)
        /// </summary>
        protected Stream Stream { get; set; }

        /// <summary>
        /// Client's connected status
        /// </summary>
        public bool IsConnected { get; protected set; }

        /// <summary>
        /// Gets the connection is over SSL or not
        /// </summary>
        public bool IsSsl { get; protected set; }

        /// <summary>
        /// The last PONG received time. If this time is before last second PING time, the client will be removed.
        /// </summary>
        internal DateTime PongTime { get; private set; }

        /// <summary>
        /// When client is disconnected and disposed,
        /// The message will be sent to all event subscribers.
        /// Sometimes multiple errors occur when the connection is failed.
        /// To avoid multiple event fires, this field is used.
        /// </summary>
        private volatile bool _disconnectedWarn;

        /// <summary>
        /// Read buffer 512 bytes
        /// </summary>
        private readonly byte[] _buffer = new byte[512];

        /// <summary>
        /// Reader class for last receiving network package
        /// </summary>
        private WebSocketReader _reader;

        /// <summary>
        /// If true, messages will be proceed async
        /// </summary>
        protected bool AsyncMessaging { get; set; }

        /// <summary>
        /// After endWrite called, this value will be set as true.
        /// This value is used for manipulating SslStream multiple write operation
        /// </summary>
        private volatile bool _writeCompleted = true;

        #endregion

        protected SocketBase()
        {
            PongTime = DateTime.UtcNow.AddSeconds(30);
            _reader = new WebSocketReader();
        }

        #region Methods

        /// <summary>
        /// Starts to read from the TCP socket
        /// </summary>
        protected void Read()
        {
            try
            {
                if (Stream == null)
                {
                    Disconnect();
                    return;
                }

                Stream.BeginRead(_buffer, 0, _buffer.Length, EndRead, null);
            }
            catch
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Ends reading from the TCP socket and process the package
        /// </summary>
        private void EndRead(IAsyncResult ar)
        {
            int read;
            try
            {
                read = Stream.EndRead(ar);
            }
            catch
            {
                read = 0;
            }

            if (read < 1)
            {
                Disconnect();
                return;
            }

            try
            {
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
            catch (Exception ex)
            {
                OnError("WSPROTOCOL_READ", ex);
                Disconnect();
            }

            Read();
        }

        /// <summary>
        /// Sends a string message to the socket client.
        /// Data must be plain text,
        /// WebSocket protocol information will be added
        /// </summary>
        public virtual bool Send(string message)
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
        public virtual async Task<bool> SendAsync(string message)
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

        private void EndWrite(IAsyncResult ar)
        {
            try
            {
                lock (Stream)
                {
                    Stream.EndWrite(ar);
                    _writeCompleted = true;
                }
            }
            catch
            {
                _writeCompleted = true;
                byte[] state = ar.AsyncState as byte[];
                WriteError(state);
                Disconnect();
            }
        }

        /// <summary>
        /// Sends prepared byte array message to the socket client.
        /// Data must be prepared for WebSocket protocol.
        /// It will be sent without any operation
        /// </summary>
        public bool Send(byte[] preparedData)
        {
            try
            {
                if (Stream == null || preparedData == null)
                    return false;

                lock (Stream)
                {
                    if (!_writeCompleted)
                        SendQueue(preparedData);
                    else
                    {
                        _writeCompleted = false;
                        Stream.BeginWrite(preparedData, 0, preparedData.Length, EndWrite, preparedData);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _writeCompleted = true;
                OnError("SEND_BYTES", ex);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Disconnects client and disposes all streams belongs it
        /// </summary>
        public virtual void Disconnect()
        {
            try
            {
                IsConnected = false;

                if (Stream != null)
                    Stream.Dispose();

                if (Client != null)
                    Client.Close();

                Client = null;
                Stream = null;
            }
            catch
            {
            }

            if (!_disconnectedWarn)
            {
                _disconnectedWarn = true;
                OnDisconnected();
            }
        }

        /// <summary>
        /// Over SslStream, writing another package before first package's callback is called, throws NotSupportedException.
        /// This method is for fixing that issue.
        /// When Send method is called before previous callback, sending operation will call this method.
        /// This method waits for callback operation async, and calls send method again.
        /// </summary>
        private void SendQueue(byte[] data)
        {
            SpinWait wait = new SpinWait();
            DateTime until = DateTime.UtcNow.AddSeconds(5);

            Task.Factory.StartNew(() =>
            {
                while (!_writeCompleted)
                {
                    wait.SpinOnce();
                    if (DateTime.UtcNow > until)
                        return;
                }

                Send(data);
            });
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Triggered when client is connected
        /// </summary>
        protected abstract void OnConnected();

        /// <summary>
        /// Triggered when client is disconnected
        /// </summary>
        protected abstract void OnDisconnected();

        /// <summary>
        /// Triggered when client is received text message
        /// </summary>
        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        /// <summary>
        /// Triggered when client is received binary message
        /// </summary>
        protected virtual void OnBinaryReceived(byte[] payload)
        {
            BinaryReceived?.Invoke(this, payload);
        }

        /// <summary>
        /// Triggered when an error is occured in this client operations
        /// </summary>
        protected abstract void OnError(string hint, Exception ex);

        /// <summary>
        /// Will be called when a write error has occured.
        /// Data is the message trying to send
        /// </summary>
        protected virtual void WriteError(byte[] data)
        {
        }

        #endregion
    }
}