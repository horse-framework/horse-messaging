using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
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
        private readonly HostListener _listener;
        private TimeSpan _minAliveHttpDuration;

        public ConnectionHandler(TwinoServer server, HostListener listener)
        {
            _server = server;
            _listener = listener;
        }

        /// <summary>
        /// Accepts new connection requests until stopped
        /// </summary>
        public async Task Handle()
        {
            _listener.KeepAliveManager = new KeepAliveManager();
            _listener.KeepAliveManager.Start(_server.Options.RequestTimeout);

            int alive = _server.Options.RequestTimeout;
            if (_server.Options.HttpConnectionTimeMax * 1000 > alive)
                alive = _server.Options.HttpConnectionTimeMax * 1000;

            _minAliveHttpDuration = TimeSpan.FromMilliseconds(alive);

            while (_server.IsRunning)
            {
                if (_listener.Listener == null)
                    break;

                try
                {
                    TcpClient tcp = await _listener.Listener.AcceptTcpClientAsync();

                    try
                    {
                        await AcceptClient(tcp);
                    }
                    catch (Exception ex)
                    {
                        if (_server.Logger != null)
                            _server.Logger.LogException("ACCEPT_CLIENT", ex);

                        //may throw exception while closing and disposing network stream
                        try
                        {
                            tcp.Close();
                        }
                        catch
                        {
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_server.Logger != null)
                        _server.Logger.LogException("ACCEPT_TCP_CLIENT", ex);
                }
            }
        }

        /// <summary>
        /// After the client connection request is accepted.
        /// Completes first operations for the client
        /// such as firewall authority, SSL authentication, WebSocket handshaking
        /// </summary>
        private async Task AcceptClient(TcpClient tcp)
        {
            await Task.Yield();

            if (_listener == null || tcp == null)
                return;

            ConnectionInfo info = new ConnectionInfo(tcp, _listener)
                                  {
                                      State = ConnectionStates.Pending,
                                      MaxAlive = DateTime.UtcNow + _minAliveHttpDuration
                                  };

            _listener.KeepAliveManager.Add(info);

            try
            {
                //ssl handshaking
                if (_listener.Options.SslEnabled)
                {
                    try
                    {
                        SslStream sslStream = _listener.Options.BypassSslValidation
                                                  ? new SslStream(tcp.GetStream(), true, (a, b, c, d) => true)
                                                  : new SslStream(tcp.GetStream(), true);

                        info.SslStream = sslStream;
                        SslProtocols protocol = GetProtocol(_listener);
                        await sslStream.AuthenticateAsServerAsync(_listener.Certificate, false, protocol, false);
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
                    info.PlainStream = tcp.GetStream();

                await ReadConnection(info);
            }
            catch (Exception ex)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("HANDLE_CONNECTION", ex);

                info.Close();
            }
        }

        /// <summary>
        /// After TCP socket is accepted and SSL handshaking is completed.
        /// Reads the HTTP Request and finishes the operation if WebSocket or HTTP Request
        /// </summary>
        private async Task ReadConnection(ConnectionInfo info)
        {
            while (true)
            {
                //read first request from http client
                RequestReader reader = new RequestReader(_server, info);

                Tuple<HttpRequest, HttpResponse> tuple = await reader.Read(info.GetStream());
                HttpRequest request = tuple.Item1;
                HttpResponse response = tuple.Item2;

                if (request == null)
                {
                    info.Close();
                    return;
                }

                bool again = await ProcessConnection(info, reader, request, response);

                if (!again)
                    break;
            }
        }

        private async Task<bool> ProcessConnection(ConnectionInfo info, RequestReader reader, HttpRequest request, HttpResponse response)
        {
            if (request == null)
            {
                info.Close();
                return false;
            }

            request.IpAddress = FindIPAddress(info.Client);

            //handle request
            if (request.IsWebSocket)
            {
                await ProcessWebSocketRequest(info, request);
                return false;
            }

            bool success = await ProcessHttpRequest(info, request, response);

            if (!success || _server.Options.HttpConnectionTimeMax == 0)
            {
                info.Close();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Process websocket connection and creates new session for the connection
        /// </summary>
        private async Task ProcessWebSocketRequest(ConnectionInfo info, HttpRequest request)
        {
            //if WebSocket is not supported, close connection
            if (_server.ClientFactory == null)
            {
                info.Close();
                return;
            }

            info.State = ConnectionStates.WebSocket;
            SocketRequestHandler handler = new SocketRequestHandler(_server, request, info.Client);
            await handler.HandshakeClient();
        }

        /// <summary>
        /// Process HTTP connection, sends response.
        /// </summary>
        private async Task<bool> ProcessHttpRequest(ConnectionInfo info, HttpRequest request, HttpResponse response)
        {
            //if HTTP is not supported, close connection
            if (_server.RequestHandler == null)
            {
                info.Close();
                return false;
            }

            info.State = ConnectionStates.Http;
            await _server.RequestHandler.RequestAsync(_server, request, response);
            ResponseWriter writer = new ResponseWriter(_server);
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


        /// <summary>
        /// Disposes connection handler and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_listener.Listener == null)
                return;

            _listener.Listener.Start();
            try
            {
                _listener.Handle.Interrupt();
            }
            catch
            {
            }

            if (_listener.KeepAliveManager != null)
                _listener.KeepAliveManager.Stop();

            _listener.KeepAliveManager = null;
            _listener.Listener = null;
            _listener.Handle = null;
        }

        private static SslProtocols GetProtocol(HostListener server)
        {
            return server.Options.SslProtocol switch
            {
                "tls" => SslProtocols.Tls,
                "tls11" => SslProtocols.Tls11,
                "tls12" => SslProtocols.Tls12,
                _ => SslProtocols.None
            };
        }
    }
}