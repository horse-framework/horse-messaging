using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.Http
{
    /// <summary>
    /// Handle status for each TCP connection.
    /// Decides if connection will be kept alive or not
    /// </summary>
    internal enum HandleStatus
    {
        /// <summary>
        /// Closes the connection
        /// </summary>
        Close,
        
        /// <summary>
        /// Does not close connection and reads next request
        /// </summary>
        ReadAgain,
        
        /// <summary>
        /// Does not close connection but does not read next request.
        /// Used when protocol is switched.
        /// </summary>
        ExitWithoutClosing
    }

    /// <summary>
    /// HTTP Protocol handler for Twino Server
    /// </summary>
    public class TwinoHttpProtocol : ITwinoProtocol
    {
        /// <summary>
        /// Protocol name
        /// </summary>
        public string Name => "http";

        /// <summary>
        /// Protocol's connection handler
        /// </summary>
        private readonly IProtocolConnectionHandler<HttpMessage> _handler;

        /// <summary>
        /// Server time updater for response time data
        /// </summary>
        private readonly Timer _timeTimer;
        
        /// <summary>
        /// Server
        /// </summary>
        private readonly ITwinoServer _server;

        /// <summary>
        /// If the request first bytes starts with an item in this list,
        /// request is HTTP protocol and we accept it.
        /// Otherwise, request isn't accepted by HTTP Protocol handler.
        /// </summary>
        private static readonly byte[][] PROTOCOL_CHECK_LIST =
        {
            Encoding.ASCII.GetBytes("GET "),
            Encoding.ASCII.GetBytes("POST "),
            Encoding.ASCII.GetBytes("PUT "),
            Encoding.ASCII.GetBytes("PATCH "),
            Encoding.ASCII.GetBytes("OPTION"),
            Encoding.ASCII.GetBytes("HEAD "),
            Encoding.ASCII.GetBytes("DELETE"),
            Encoding.ASCII.GetBytes("TRACE "),
            Encoding.ASCII.GetBytes("CONNEC"),
        };

        /// <summary>
        /// Options of the HTTP server
        /// </summary>
        public HttpOptions Options { get; set; }

        public TwinoHttpProtocol(ITwinoServer server, IProtocolConnectionHandler<HttpMessage> handler, HttpOptions options)
        {
            Options = options;
            _server = server;
            _handler = handler;

            PredefinedHeaders.SERVER_TIME_CRLF = Encoding.UTF8.GetBytes("Date: " + DateTime.UtcNow.ToString("R") + "\r\n");
            _timeTimer = new Timer(s => PredefinedHeaders.SERVER_TIME_CRLF = Encoding.UTF8.GetBytes("Date: " + DateTime.UtcNow.ToString("R") + "\r\n"), "", 1000, 1000);
        }

        /// <summary>
        /// Checks if data is belong this protocol.
        /// </summary>
        /// <param name="info">Connection information</param>
        /// <param name="data">Data is first 8 bytes of the first received message from the client</param>
        /// <returns></returns>
        public async Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data)
        {
            ProtocolHandshakeResult result = new ProtocolHandshakeResult();
            result.Accepted = CheckProtocol(data);
            if (result.Accepted)
                result.ReadAfter = true;

            info.State = ConnectionStates.Http;
            return await Task.FromResult(result);
        }

        /// <summary>
        /// When protocol is switched to this protocol from another protocol
        /// </summary>
        public Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, ConnectionData data)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// After protocol handshake is completed, this method is called to handle events for the specified client
        /// </summary>
        public async Task HandleConnection(IConnectionInfo info, ProtocolHandshakeResult handshakeResult)
        {
            HttpReader reader = new HttpReader(Options);
            HttpWriter writer = new HttpWriter(Options);
            reader.HandshakeResult = handshakeResult;

            HandleStatus status;
            do
            {
                HttpMessage message = await reader.Read(info.GetStream());

                if (message.Request != null)
                    message.Request.IpAddress = FindIPAddress(info.Client);

                status = await ProcessMessage(info, writer, message, reader.ContentLength);

                if (status == HandleStatus.ReadAgain)
                    reader.Reset();
            } while (status == HandleStatus.ReadAgain);

            if (status == HandleStatus.Close)
                info.Close();
        }

        /// <summary>
        /// Checks and returns true if data contains protocol's expected data
        /// </summary>
        private static bool CheckProtocol(byte[] data)
        {
            Span<byte> span = data;
            foreach (byte[] arr in PROTOCOL_CHECK_LIST)
            {
                if (span.StartsWith(arr))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Process the HTTP message
        /// </summary>
        private async Task<HandleStatus> ProcessMessage(IConnectionInfo info, HttpWriter writer, HttpMessage message, int contentLength)
        {
            if (message.Request == null)
                return HandleStatus.Close;

            message.Request.ContentLength = contentLength;
            message.Response.Request = message.Request;

            if (message.Response.StatusCode > 0)
            {
                await writer.Write(message.Response);
                return HandleStatus.Close;
            }

            if (!string.IsNullOrEmpty(message.Request.Upgrade))
            {
                if (!string.IsNullOrEmpty(message.Request.Upgrade))
                {
                    ConnectionData data = new ConnectionData();
                    data.Path = message.Request.Path;
                    data.Method = message.Request.Method;
                    data.SetProperties(message.Request.Headers);

                    await _server.SwitchProtocol(info, message.Request.Upgrade, data);
                    return HandleStatus.ExitWithoutClosing;
                }
            }

            HttpReader.ReadContent(message.Request);

            try
            {
                await _handler.Received(_server, info, null, message);
            }
            catch (Exception ex)
            {
                if (_server.Logger != null)
                    _server.Logger.LogException("Unhandled Exception", ex);
            }

            await writer.Write(message.Response);

            //stay alive, if keep alive active and response has stream
            bool keep = Options.HttpConnectionTimeMax > 0 && message.Response.HasStream();
            return keep ? HandleStatus.ReadAgain : HandleStatus.Close;
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