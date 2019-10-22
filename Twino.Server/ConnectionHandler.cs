using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core.Http;
using Twino.Server.Http;
using Twino.Server.WebSockets;

namespace Twino.Server
{
    /// <summary>
    /// Accept TCP connections
    /// </summary>
    public class ConnectionHandler
    {
        private readonly TwinoServer _server;
        private readonly InnerServer _inner;
        private TimeSpan _minAliveHttpDuration;

        public ConnectionHandler(TwinoServer server, InnerServer inner)
        {
            _server = server;
            _inner = inner;
        }

        /// <summary>
        /// Accepts new connection requests until stopped
        /// </summary>
        public async Task Handle()
        {
            _inner.TimeoutHandler = new TimeoutHandler(_server.Options.RequestTimeout);
            _inner.TimeoutHandler.Run();

            int alive = _server.Options.RequestTimeout;
            if (_server.Options.HttpConnectionTimeMax * 1000 > alive)
                alive = _server.Options.HttpConnectionTimeMax * 1000;

            _minAliveHttpDuration = TimeSpan.FromMilliseconds(alive);

            while (_server.IsRunning)
            {
                if (_inner.Listener == null)
                    break;

                try
                {
                    TcpClient tcp = await _inner.Listener.AcceptTcpClientAsync();
                    ThreadPool.UnsafeQueueUserWorkItem(async t =>
                    {
                        TcpClient client = (TcpClient) t;
                        try
                        {
                            await AcceptClient(client);
                        }
                        catch (Exception ex)
                        {
                            if (_server.Logger != null)
                                _server.Logger.LogException("ACCEPT_CLIENT", ex);

                            client.Close();
                        }
                    }, tcp);
                }
                catch (Exception ex)
                {
                    if (_server.Logger != null)
                        _server.Logger.LogException("ACCEPT_TCP_CLIENT", ex);
                }
            }
        }

        /// <summary>
        /// Disposes connection handler and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_inner.Listener == null)
                return;

            _inner.Listener.Start();
            try
            {
                _inner.Handle.Interrupt();
            }
            catch
            {
            }

            if (_inner.TimeoutHandler != null)
                _inner.TimeoutHandler.Running = false;

            _inner.TimeoutHandler = null;
            _inner.Listener = null;
            _inner.Handle = null;
        }

        private static SslProtocols GetProtocol(InnerServer server)
        {
            return server.Options.SslProtocol switch
            {
                "tls" => SslProtocols.Tls,
                "tls11" => SslProtocols.Tls11,
                "tls12" => SslProtocols.Tls12,
                _ => SslProtocols.None
            };
        }

        /// <summary>
        /// After the client connection request is accepted.
        /// Completes first operations for the client
        /// such as firewall authority, SSL authentication, WebSocket handshaking
        /// </summary>
        private async Task AcceptClient(TcpClient tcp)
        {
            await Task.Yield();

            if (_inner == null || tcp == null)
                return;

            HandshakeInfo handshake = new HandshakeInfo
                                      {
                                          Client = tcp,
                                          Server = _inner,
                                          State = ConnectionStates.Pending,
                                          MaxAlive = DateTime.UtcNow + _minAliveHttpDuration
                                      };

            if (_inner.TimeoutHandler != null)
                _inner.TimeoutHandler.Add(handshake);

            //ssl handshaking
            if (_inner.Options.SslEnabled)
            {
                try
                {
                    SslStream sslStream = _inner.Options.BypassSslValidation
                                              ? new SslStream(tcp.GetStream(), true, (a, b, c, d) => true)
                                              : new SslStream(tcp.GetStream(), true);

                    handshake.SslStream = sslStream;
                    SslProtocols protocol = GetProtocol(_inner);
                    await sslStream.AuthenticateAsServerAsync(_inner.Certificate, false, protocol, false);
                }
                catch (Exception ex)
                {
                    if (_server.Logger != null)
                        _server.Logger.LogException("SSL_HANDSHAKE", ex);

                    tcp.Close();
                    return;
                }
            }
            else
                handshake.PlainStream = tcp.GetStream();

            await FinishAccept(handshake);
        }

        /// <summary>
        /// After TCP socket is accepted and SSL handshaking is completed.
        /// Reads the HTTP Request and finishes the operation if WebSocket or HTTP Request
        /// </summary>
        private async Task FinishAccept(HandshakeInfo handshake)
        {
            //read first request from http client
            RequestReader reader = new RequestReader(_server, handshake.Server);

            Tuple<HttpRequest, HttpResponse> tuple = await reader.Read(handshake.GetStream());
            HttpRequest request = tuple.Item1;
            HttpResponse response = tuple.Item2;

            try
            {
                if (request == null)
                {
                    handshake.Close();
                    return;
                }

                request.IpAddress = FindIPAddress(handshake.Client);

                //handle request
                if (request.IsWebSocket)
                    await ProcessWebSocketRequest(handshake, request);
                else
                {
                    bool success = await ProcessHttpRequest(handshake, request, response);
                    if (success && _server.Options.HttpConnectionTimeMax > 0)
                        await FinishAccept(handshake);
                    else
                        handshake.Close();
                }
            }
            catch (Exception ex)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("HANDLE_CONNECTION", ex);

                handshake.Close();
            }
        }

        /// <summary>
        /// Process websocket connection and creates new session for the connection
        /// </summary>
        private async Task ProcessWebSocketRequest(HandshakeInfo handshake, HttpRequest request)
        {
            //if WebSocket is not supported, close connection
            if (_server.ClientFactory == null)
            {
                handshake.Close();
                return;
            }

            handshake.State = ConnectionStates.WebSocket;
            SocketRequestHandler handler = new SocketRequestHandler(_server, request, handshake.Client);
            await handler.HandshakeClient();
        }

        /// <summary>
        /// Process HTTP connection, sends response.
        /// </summary>
        private async Task<bool> ProcessHttpRequest(HandshakeInfo handshake, HttpRequest request, HttpResponse response)
        {
            //if HTTP is not supported, close connection
            if (_server.RequestHandler == null)
            {
                handshake.Close();
                return false;
            }

            handshake.State = ConnectionStates.Http;
            await _server.RequestHandler.RequestAsync(_server, request, response);
            ResponseWriter writer = new ResponseWriter(response);
            await writer.Write(response);

            return _server.Options.HttpConnectionTimeMax > 0;
        }

        /// <summary>
        /// Finds the IP Address of the TCP client socket
        /// </summary>
        private static string FindIPAddress(TcpClient tcp)
        {
            return tcp.Client.RemoteEndPoint.ToString().Split(':')[0];
        }
    }
} 