using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client;

/// <summary>
/// Represents a single tcp connection to horse messaging server
/// </summary>
public class HorseSocket : ClientSocketBase<HorseMessage>
{
    private readonly HorseClient _client;
    internal bool IsConnecting { get; private set; }

    private readonly HorseProtocolReader _reader = new();

    internal HorseSocket(HorseClient client, ConnectionData data)
    {
        _client = client;

        Data.Method = "CONNECT";
        Data.Path = "/";
        Data.SetProperties(data.Properties);
    }

    #region Ping - Pong

    /// <summary>
    /// Sends a PING message
    /// </summary>
    public override void Ping()
    {
        if (_client.SwitchingProtocol != null)
            _client.SwitchingProtocol.Ping();
        else
            Send(PredefinedMessages.PING);
    }

    /// <summary>
    /// Sends a PONG message
    /// </summary>
    public override void Pong(object pingMessage = null)
    {
        if (_client.SwitchingProtocol != null)
            _client.SwitchingProtocol.Pong();
        else
            Send(PredefinedMessages.PONG);
    }

    #endregion

    /// <summary>
    /// Connects to well defined remote host
    /// </summary>
    public override void Connect(DnsInfo host)
    {
        try
        {
            IsConnecting = true;
            Client = new TcpClient();

            if (_client.NoDelay.HasValue)
                Client.NoDelay = _client.NoDelay.Value;

            Client.Connect(host.IPAddress, host.Port);
            IsConnected = true;
            IsSsl = host.SSL;

            //creates SSL Stream or Insecure stream
            if (host.SSL)
            {
                SslStream sslStream = new SslStream(Client.GetStream(), true, CertificateCallback);

                X509Certificate2Collection certificates = null;
                if (Certificate != null)
                {
                    certificates = new X509Certificate2Collection();
                    certificates.Add(Certificate);
                }

                sslStream.AuthenticateAsClient(host.Hostname, certificates, false);
                Stream = sslStream;
            }
            else
                Stream = Client.GetStream();

            if (_client.SwitchingProtocol != null)
                _client.SwitchingProtocol.ClientProtocolHandshake(Data, Stream).Wait();
            else
            {
                Stream.Write(PredefinedMessages.PROTOCOL_BYTES_V3);
                SendInfoMessage(host).Wait();

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES_V3.Length];
                int len = Stream.Read(buffer, 0, buffer.Length);

                CheckProtocolResponse(buffer, len);
            }

            _ = Start();
        }
        catch
        {
            Disconnect();
            throw;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <summary>
    /// Connects to well defined remote host
    /// </summary>
    public override async Task ConnectAsync(DnsInfo host)
    {
        try
        {
            IsConnecting = true;
            Client = new TcpClient();

            if (_client.NoDelay.HasValue)
                Client.NoDelay = _client.NoDelay.Value;

            await Client.ConnectAsync(host.IPAddress, host.Port);
            IsConnected = true;
            IsSsl = host.SSL;

            //creates SSL Stream or Insecure stream
            if (host.SSL)
            {
                SslStream sslStream = new SslStream(Client.GetStream(), true, CertificateCallback);

                X509Certificate2Collection certificates = null;
                if (Certificate != null)
                {
                    certificates = new X509Certificate2Collection();
                    certificates.Add(Certificate);
                }

                await sslStream.AuthenticateAsClientAsync(host.Hostname, certificates, false);
                Stream = sslStream;
            }
            else
                Stream = Client.GetStream();

            if (_client.SwitchingProtocol != null)
                await _client.SwitchingProtocol.ClientProtocolHandshake(Data, Stream);
            else
            {
                await Stream.WriteAsync(PredefinedMessages.PROTOCOL_BYTES_V3);
                await SendInfoMessage(host);

                //Reads the protocol response
                byte[] buffer = new byte[PredefinedMessages.PROTOCOL_BYTES_V3.Length];
                int len = await Stream.ReadAsync(buffer);

                CheckProtocolResponse(buffer, len);
            }

            _ = Start();
        }
        catch
        {
            Disconnect();
            throw;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    /// <summary>
    /// Checks protocol response message from server.
    /// If protocols are not matched, an exception is thrown 
    /// </summary>
    private static void CheckProtocolResponse(byte[] buffer, int length)
    {
        if (length < PredefinedMessages.PROTOCOL_BYTES_V3.Length)
            throw new InvalidOperationException("Unexpected server response");

        for (int i = 0; i < PredefinedMessages.PROTOCOL_BYTES_V3.Length; i++)
            if (PredefinedMessages.PROTOCOL_BYTES_V3[i] != buffer[i])
                throw new NotSupportedException("Unsupported Horse Protocol version. Server supports: " + Encoding.UTF8.GetString(buffer));
    }

    /// <summary>
    /// Startes to read messages from server
    /// </summary>
    private async Task Start()
    {
        OnConnected();
        _ = _client.OnConnected();

        try
        {
            while (IsConnected)
                await Read();
        }
        catch
        {
            Disconnect();
        }
    }

    /// <summary>
    /// Sends connection data message to server, called right after procotol handshaking completed.
    /// </summary>
    /// <returns></returns>
    private async Task SendInfoMessage(DnsInfo dns)
    {
        if (Data?.Properties == null ||
            Data.Properties.Count < 1 &&
            string.IsNullOrEmpty(Data.Method) &&
            string.IsNullOrEmpty(Data.Path) &&
            string.IsNullOrEmpty(_client.ClientId))
            return;

        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Server;
        message.ContentType = KnownContentTypes.Hello;

        message.Content = new MemoryStream();

        Data.Path = string.IsNullOrEmpty(dns.Path) ? "/" : dns.Path;
        if (string.IsNullOrEmpty(Data.Method))
            Data.Method = "NONE";

        string first = Data.Method + " " + Data.Path + "\r\n";
        await message.Content.WriteAsync(Encoding.UTF8.GetBytes(first));

        Data.Properties[HorseHeaders.CLIENT_ID] = _client.ClientId;

        foreach (var prop in Data.Properties)
        {
            string line = prop.Key + ": " + prop.Value + "\r\n";
            byte[] lineData = Encoding.UTF8.GetBytes(line);
            await message.Content.WriteAsync(lineData);
        }

        await SendAsync(message);
    }

    /// <summary>
    /// Reads messages from server
    /// </summary>
    /// <returns></returns>
    protected override async Task Read()
    {
        HorseMessage message;

        if (_client.SwitchingProtocol != null)
            message = await _client.SwitchingProtocol.Read(Stream);
        else
            message = await _reader.Read(Stream);

        if (message == null)
        {
            Disconnect();
            return;
        }

        if (SmartHealthCheck)
            KeepAlive();

        _ = _client.OnMessageReceived(message);
    }

    /// <summary>
    /// Client disconnected from the server
    /// </summary>
    protected override void OnDisconnected()
    {
        base.OnDisconnected();
        _client.OnDisconnected();
    }

    /// <summary>
    /// Sends a Horse message
    /// </summary>
    public async Task<HorseResult> SendAsync(HorseMessage message, IList<KeyValuePair<string, string>> additionalHeaders = null)
    {
        message.SetSource(_client.ClientId);

        if (string.IsNullOrEmpty(message.MessageId))
            message.SetMessageId(_client.UniqueIdGenerator.Create());

        bool sent;
        if (_client.SwitchingProtocol != null)
            sent = _client.SwitchingProtocol.Send(message, additionalHeaders);
        else
        {
            byte[] data = HorseProtocolWriter.Create(message, additionalHeaders);
            sent = await SendAsync(data);
        }

        return sent ? HorseResult.Ok() : new HorseResult(HorseResultCode.SendError);
    }

    /// <summary>
    /// Send bulk messages asynchoronously.
    /// Callback method result corresponds if the data is written succesfully to the tcp connection or not.
    /// It does not track acknowledge messages from the server.
    /// </summary>
    public void SendBulk(IEnumerable<HorseMessage> messages, Action<HorseMessage, bool> sendCallback)
    {
        if (_client.SwitchingProtocol != null)
            throw new NotSupportedException("Sending Bulk messages are not supported via Switching Protocol");

        foreach (HorseMessage message in messages)
        {
            message.SetSource(_client.ClientId);

            if (string.IsNullOrEmpty(message.MessageId))
                message.SetMessageId(_client.UniqueIdGenerator.Create());

            byte[] data = HorseProtocolWriter.Create(message);
            Send(data, result => sendCallback?.Invoke(message, result));
        }
    }
}