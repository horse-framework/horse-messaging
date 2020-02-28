using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;
using Timer = System.Timers.Timer;

namespace Twino.Server
{
    /// <summary>
    /// Crated for catching twino inner exceptions with events in TwinoServer
    /// </summary>
    public delegate void TwinoInnerExceptionHandler(TwinoServer server, Exception ex);

    /// <summary>
    /// Twino TCP Server
    /// Listens all TCP Connections and routes to requests protocols
    /// </summary>
    public class TwinoServer : ITwinoServer
    {
        #region Properties

        /// <summary>
        /// Pinger for piped clients that connect and stay alive for a long time
        /// </summary>
        public IPinger Pinger { get; private set; }

        /// <summary>
        /// Logger class for Server operations.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Server options. Can set programmatically with constructor parameter
        /// Or can set with "rimserver.json", "server.json" or "rim.json" options filename
        /// </summary>
        public ServerOptions Options { get; }

        /// <summary>
        /// Server status, If true, server is listening for new connections
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Server's supported protocols
        /// </summary>
        internal ITwinoProtocol[] Protocols { get; private set; } = new ITwinoProtocol[0];

        //creating string from DateTime object per request uses some cpu and time (1 sec full cpu for 10million times)
        /// <summary>
        /// Server time timer
        /// </summary>
        private Timer _timeTimer;

        /// <summary>
        /// TcpListener for HttpServer
        /// </summary>
        private List<ConnectionHandler> _handlers = new List<ConnectionHandler>();

        /// <summary>
        /// Triggered when inner exception is raised in twino server
        /// </summary>
        public event TwinoInnerExceptionHandler OnInnerException;

        /// <summary>
        /// Triggered when the server is started
        /// </summary>
        public event Action<TwinoServer> OnStarted;

        /// <summary>
        /// Triggered when the server is stopped
        /// </summary>
        public event Action<TwinoServer> OnStopped;

        #endregion

        #region Constructors

        /// <summary>
        /// Creates new TwinoServer instance.
        /// </summary>
        public TwinoServer() : this(default(ServerOptions))
        {
        }

        /// <summary>
        /// Creates new TwinoServer instance.
        /// </summary>
        /// <param name="optionsFilename">Server options</param>
        public TwinoServer(string optionsFilename)
        {
            Options = ServerOptions.LoadFromFile(optionsFilename);
        }

        /// <summary>
        /// Creates new TwinoServer instance.
        /// </summary>
        /// <param name="options">Server options</param>
        public TwinoServer(ServerOptions options)
        {
            if (options == null)
                Options = ServerOptions.LoadFromFile();
            else
                Options = options;
        }

        #endregion

        #region Start - Stop

        /// <summary>
        /// Block main thread, typical thread sleep
        /// </summary>
        public void BlockWhileRunning()
        {
            while (IsRunning)
                Thread.Sleep(100);
        }

        /// <summary>
        /// Block main thread, typical task delay
        /// </summary>
        public async Task BlockWhileRunningAsync()
        {
            while (IsRunning)
                await Task.Delay(250);
        }

        /// <summary>
        /// Starts server and listens specified port without ssl
        /// </summary>
        /// <param name="port"></param>
        public void Start(int port)
        {
            Options.Hosts = new List<HostOptions>();
            HostOptions host = new HostOptions
            {
                Port = port,
                SslEnabled = false
            };

            Options.Hosts.Add(host);

            Start();
        }

        /// <summary>
        /// Starts server and listens new connection requests
        /// </summary>
        public void Start()
        {
            if (IsRunning)
                throw new InvalidOperationException("Stop the HttpServer before restart");

            if (Options.Hosts == null || Options.Hosts.Count == 0)
                throw new ArgumentNullException($"Hosts", "There is no host to listen. Add hosts to Twino Options");

            if (_timeTimer != null)
            {
                _timeTimer.Stop();
                _timeTimer.Dispose();
            }

            IsRunning = true;
            _handlers = new List<ConnectionHandler>();

            foreach (HostOptions host in Options.Hosts)
            {
                HostListener server = new HostListener();
                server.Options = host;

                if (host.SslEnabled && !string.IsNullOrEmpty(host.SslCertificate))
                {
                    server.Certificate = string.IsNullOrEmpty(host.CertificateKey)
                                             ? new X509Certificate2(host.SslCertificate)
                                             : new X509Certificate2(host.SslCertificate, host.CertificateKey);
                }

                server.Listener = new TcpListener(IPAddress.Any, host.Port);

                if (Options.MaximumPendingConnections == 0)
                    server.Listener.Start();
                else
                    server.Listener.Start(Options.MaximumPendingConnections);

                ConnectionHandler handler = new ConnectionHandler(this, server);
                server.Handle = new Thread(() => _ = handler.Handle());
                server.Handle.IsBackground = true;
                server.Handle.Priority = ThreadPriority.Highest;
                server.Handle.Start();
                _handlers.Add(handler);
            }

            IsRunning = true;
            //if websocket ping is activated, starts pinger
            if (Options.PingInterval > 0)
            {
                Pinger = new Pinger(this, TimeSpan.FromSeconds(Options.PingInterval));
                Pinger.Start();
            }

            OnStarted?.Invoke(this);
        }

        /// <summary>
        /// Stops accepting connections.
        /// But does not disconnect connected clients.
        /// In order to disconnect all clients, you need to do it manually
        /// You can use a ClientContainer implementation to do it easily
        /// </summary>
        public void Stop()
        {
            IsRunning = false;

            //stop server time creator timer
            if (_timeTimer != null)
            {
                _timeTimer.Stop();
                _timeTimer.Dispose();
                _timeTimer = null;
            }

            //stop websocket pinger
            if (Pinger != null)
            {
                Pinger.Stop();
                Pinger = null;
            }

            //stop and dispose all listeners (for all ports)
            foreach (ConnectionHandler handler in _handlers)
                handler.Dispose();

            _handlers.Clear();

            OnStopped?.Invoke(this);
        }

        #endregion

        #region Protocols

        /// <summary>
        /// Uses the protocol for new TCP connections that request the protocol
        /// </summary>
        public void UseProtocol(ITwinoProtocol protocol)
        {
            List<ITwinoProtocol> list = Protocols.ToList();

            ITwinoProtocol old = list.FirstOrDefault(x => x.Name.Equals(protocol.Name, StringComparison.InvariantCultureIgnoreCase));
            if (old != null)
                list.Remove(old);

            list.Add(protocol);
            Protocols = list.ToArray();
        }

        /// <summary>
        /// Switches client's protocol to new protocol (finds by name)
        /// </summary>
        public async Task SwitchProtocol(IConnectionInfo info, string newProtocolName, ConnectionData data)
        {
            foreach (ITwinoProtocol protocol in Protocols)
            {
                if (protocol.Name.Equals(newProtocolName, StringComparison.InvariantCultureIgnoreCase))
                {
                    ProtocolHandshakeResult hsresult = await protocol.SwitchTo(info, data);
                    if (!hsresult.Accepted)
                    {
                        info.Close();
                        return;
                    }

                    ITwinoProtocol previous = info.Protocol;
                    info.Protocol = protocol;
                    info.Socket = hsresult.Socket;

                    if (info.Socket != null)
                        info.Socket.SetOnConnected();

                    if (hsresult.Response != null)
                        await info.GetStream().WriteAsync(hsresult.Response);

                    if (info.Socket != null)
                        info.Socket.SetOnProtocolSwitched(previous, info.Protocol);

                    await protocol.HandleConnection(info, hsresult);
                    return;
                }
            }
        }

        /// <summary>
        /// Finds protocol by name
        /// </summary>
        public ITwinoProtocol FindProtocol(string name)
        {
            return Protocols.FirstOrDefault(x => x.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        #endregion

        internal void RaiseException(Exception ex)
        {
            OnInnerException?.Invoke(this, ex);
        }
    }
}