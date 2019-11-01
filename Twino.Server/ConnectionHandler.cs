using System;
using System.IO;
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
        /// <summary>
        /// twino server of connection handler
        /// </summary>
        private readonly TwinoServer _server;

        /// <summary>
        /// Host listener object of connection handler
        /// </summary>
        private readonly HostListener _listener;

        /// <summary>
        /// Maximum alive duration for a client.
        /// This value generally equals to HttpConnectionTimeMax.
        /// But when connection keep-alive disabled, this value equals to RequestTimeout
        /// </summary>
        private TimeSpan _manAliveHttpDuration;

        /// <summary>
        /// Response writing is not request or client specified.
        /// We can reuse same instance instead of creating new one for each request
        /// </summary>
        private readonly ResponseWriter _writer;

        public ConnectionHandler(TwinoServer server, HostListener listener)
        {
            _server = server;
            _listener = listener;
            _writer = new ResponseWriter(server);
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

            _manAliveHttpDuration = TimeSpan.FromMilliseconds(alive);

            while (_server.IsRunning)
            {
                if (_listener.Listener == null)
                    break;

                try
                {
                    TcpClient tcp = await _listener.Listener.AcceptTcpClientAsync();
                    ThreadPool.UnsafeQueueUserWorkItem(async t =>
                    {
                        try
                        {
                            await AcceptClient(tcp);
                        }
                        catch (Exception ex)
                        {
                            if (_server.Logger != null)
                                _server.Logger.LogException("Unhandled Exception", ex);
                        }
                    }, tcp, false);
                }
                catch
                {
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
            if (_listener == null)
                return;

            ConnectionInfo info = new ConnectionInfo(tcp, _listener)
                                  {
                                      State = ConnectionStates.Pending,
                                      MaxAlive = DateTime.UtcNow + _manAliveHttpDuration
                                  };

            _listener.KeepAliveManager.Add(info);

            try
            {
                //ssl handshaking
                if (_listener.Options.SslEnabled)
                {
                    SslStream sslStream = _listener.Options.BypassSslValidation
                                              ? new SslStream(tcp.GetStream(), true, (a, b, c, d) => true)
                                              : new SslStream(tcp.GetStream(), true);

                    info.SslStream = sslStream;
                    SslProtocols protocol = GetProtocol(_listener);
                    await sslStream.AuthenticateAsServerAsync(_listener.Certificate, false, protocol, false);
                }

                //read first request from http client
                HttpReader reader = new HttpReader(_server.Options, info.Server.Options);
                bool keepReading;
                do
                {
                    keepReading = await ReadConnection(info, reader);

                    if (keepReading)
                        reader.Reset();
                    
                } while (keepReading);
            }
            catch (SocketException)
            {
                info.Close();
            }
            catch (IOException)
            {
                info.Close();
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
        private async Task<bool> ReadConnection(ConnectionInfo info, HttpReader reader)
        {
            Tuple<HttpRequest, HttpResponse> tuple = await reader.Read(info.GetStream());
            HttpRequest request = tuple.Item1;
            HttpResponse response = tuple.Item2;

            if (request == null)
            {
                info.Close();
                return false;
            }

            request.ContentLength = reader.ContentLength;
            response.Request = request;

            if (response.StatusCode > 0)
            {
                await _writer.Write(response);
                info.Close();
                return false;
            }

            reader.ReadContent(request);
            bool again = await ProcessConnection(info, request, response);

            return again;
        }

        /// <summary>
        /// Process the request and redirect it to websocket or http handler
        /// </summary>
        private async Task<bool> ProcessConnection(ConnectionInfo info, HttpRequest request, HttpResponse response)
        {
            request.IpAddress = FindIPAddress(info.Client);

            //handle request
            if (request.IsWebSocket)
            {
                await ProcessWebSocketRequest(info, request);
                return false;
            }

            bool success = await ProcessHttpRequest(info, request, response);

            if (!success)
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

            await Task.Yield();

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
            await _writer.Write(response);

            //stay alive, if keep alive active and response has stream
            return _server.Options.HttpConnectionTimeMax > 0 && response.HasStream();
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

        /// <summary>
        /// Finds supported SSL protocol from server options
        /// </summary>
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