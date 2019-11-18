using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Twino.Core;
using Twino.Core.Protocols;

namespace Twino.Protocols.Http
{
    internal enum HandleStatus
    {
        Close,
        ReadAgain,
        ExitWithoutClosing
    }

    public class TwinoHttpProtocol : ITwinoProtocol<HttpMessage>
    {
        public string Name => "http";
        public byte[] PingMessage => null;
        public byte[] PongMessage => null;

        public IProtocolConnectionHandler<HttpMessage> Handler { get; }

        private readonly Timer _timeTimer;
        private readonly ITwinoServer _server;

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

        public HttpOptions Options { get; set; }

        public TwinoHttpProtocol(ITwinoServer server, IProtocolConnectionHandler<HttpMessage> handler, HttpOptions options)
        {
            Options = options;
            _server = server;
            Handler = handler;

            PredefinedHeaders.SERVER_TIME_CRLF = Encoding.UTF8.GetBytes("Date: " + DateTime.UtcNow.ToString("R") + "\r\n");
            _timeTimer = new Timer(s => PredefinedHeaders.SERVER_TIME_CRLF = Encoding.UTF8.GetBytes("Date: " + DateTime.UtcNow.ToString("R") + "\r\n"), "", 1000, 1000);
        }

        public async Task<ProtocolHandshakeResult> Handshake(IConnectionInfo info, byte[] data)
        {
            ProtocolHandshakeResult result = new ProtocolHandshakeResult();
            result.Accepted = CheckSync(data);
            if (result.Accepted)
                result.ReadAfter = true;

            info.State = ConnectionStates.Http;
            return await Task.FromResult(result);
        }

        public Task<ProtocolHandshakeResult> SwitchTo(IConnectionInfo info, Dictionary<string, string> properties)
        {
            throw new NotSupportedException();
        }

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

                status = await ProcessMessage(info, reader, writer, message, reader.ContentLength);

                if (status == HandleStatus.ReadAgain)
                    reader.Reset();
            } while (status == HandleStatus.ReadAgain);

            if (status == HandleStatus.Close)
                info.Close();
        }

        private static bool CheckSync(byte[] data)
        {
            Span<byte> span = data;
            foreach (byte[] arr in PROTOCOL_CHECK_LIST)
            {
                if (span.StartsWith(arr))
                    return true;
            }

            return false;
        }

        public IProtocolMessageReader<HttpMessage> CreateReader()
        {
            HttpReader reader = new HttpReader(Options);
            return reader;
        }

        public IProtocolMessageWriter<HttpMessage> CreateWriter()
        {
            HttpWriter writer = new HttpWriter(Options);
            return writer;
        }

        private async Task<HandleStatus> ProcessMessage(IConnectionInfo info, HttpReader reader, HttpWriter writer, HttpMessage message, int contentLength)
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

            if (message.Response.StatusCode == HttpStatusCode.SwitchingProtocols)
            {
                string protocolName;
                message.Request.Headers.TryGetValue(HttpHeaders.UPGRADE, out protocolName);
                if (!string.IsNullOrEmpty(protocolName))
                {
                    message.Request.Headers.Add("Twino-Method", message.Request.Method);
                    message.Request.Headers.Add("Twino-Path", message.Request.Path);
                    await _server.SwitchProtocol(info, protocolName, message.Request.Headers);
                    return HandleStatus.ExitWithoutClosing;
                }

                return HandleStatus.Close;
            }

            reader.ReadContent(message.Request);

            try
            {
                await Handler.Received(_server, info, null, message);
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