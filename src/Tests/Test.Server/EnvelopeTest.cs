using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Test.Common;
using Xunit;

namespace Test.Server;

/// <summary>
/// Envelope messages: inner messages are processed one by one, a nested envelope is rejected.
/// A nested envelope used to recurse on the same stack and crashed the server process at about 5000 levels.
/// </summary>
public class EnvelopeTest
{
    //HorseLogEvents.MessageProcess (internal)
    private const int MessageProcessEvent = 1103;

    private sealed class RecordingErrorHandler : IErrorHandler
    {
        public ConcurrentQueue<(int EventId, Exception Exception)> Errors { get; } = new();

        public void Error(HorseLogLevel logLevel, int eventId, string message, Exception exception)
        {
            Errors.Enqueue((eventId, exception));
        }
    }

    private static async Task<HorseClient> Connect(int port, string id)
    {
        HorseClient client = new HorseClient();
        client.ClientId = id;
        client.ResponseTimeout = TimeSpan.FromSeconds(3);
        await client.ConnectAsync("horse://localhost:" + port);
        Assert.True(client.IsConnected);
        return client;
    }

    private static HorseMessage Direct(string target)
    {
        HorseMessage message = new HorseMessage(MessageType.DirectMessage, target);
        message.SetMessageId(Guid.NewGuid().ToString("N"));
        message.SetStringContent("hello");
        return message;
    }

    private static HorseMessage Envelope(HorseMessage inner, string messageId = null)
    {
        HorseMessage envelope = new HorseMessage(MessageType.Envelope, "*", 0);
        envelope.SetMessageId(messageId ?? Guid.NewGuid().ToString("N"));
        envelope.Content = new MemoryStream(HorseProtocolWriter.Create(inner));
        envelope.CalculateLengths();
        return envelope;
    }

    private static Func<int> CountDirectMessages(HorseClient client)
    {
        int count = 0;
        client.MessageReceived += (_, m) =>
        {
            if (m.Type == MessageType.DirectMessage)
                Interlocked.Increment(ref count);
        };

        return () => Volatile.Read(ref count);
    }

    [Fact]
    public async Task Envelope_InnerMessagesAreDelivered()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = await Connect(port, "sender");
            HorseClient target = await Connect(port, "target");
            Func<int> received = CountDirectMessages(target);

            HorseResult result = await sender.SendEnvelopedAsync([Direct("target"), Direct("target")]);
            Assert.Equal(HorseResultCode.Ok, result.Code);

            await TestHorseRider.WaitUntil(() => received() == 2);
            Assert.Equal(2, received());
        });
    }

    [Fact]
    public async Task NestedEnvelope_IsRejected_AndConnectionIsClosed()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            RecordingErrorHandler errors = new RecordingErrorHandler();
            server.Rider.ErrorHandlers.Add(errors);

            HorseClient sender = await Connect(port, "sender");
            HorseClient target = await Connect(port, "target");
            Func<int> received = CountDirectMessages(target);

            MessagingClient senderOnServer = server.Rider.Client.Find("sender");
            Assert.NotNull(senderOnServer);

            await sender.SendAsync(Envelope(Envelope(Direct("target"))), CancellationToken.None);

            await TestHorseRider.WaitUntil(() => !senderOnServer.IsConnected);
            Assert.False(senderOnServer.IsConnected);
            Assert.Contains(errors.Errors, x => x.EventId == MessageProcessEvent && x.Exception is InvalidOperationException);

            //the message in the nested envelope is not processed
            await Task.Delay(300);
            Assert.Equal(0, received());

            //other clients are served
            HorseClient other = await Connect(port, "other");
            await other.SendAsync(Direct("target"), CancellationToken.None);
            await TestHorseRider.WaitUntil(() => received() == 1);
            Assert.Equal(1, received());
        });
    }

    [Fact]
    public async Task DeeplyNestedEnvelope_DoesNotCrashTheServer()
    {
        await TestHorseRider.RunWith(async (server, port) =>
        {
            HorseClient sender = await Connect(port, "sender");
            MessagingClient senderOnServer = server.Rider.Client.Find("sender");

            //5000 levels (a 60 KB message) crashed the process with a stack overflow before nested envelopes were rejected.
            //Short ids keep the message small, each level copies all inner levels.
            HorseMessage message = Direct("nobody");
            for (int i = 0; i < 5000; i++)
                message = Envelope(message, "e");

            await sender.SendAsync(message, CancellationToken.None);

            await TestHorseRider.WaitUntil(() => !senderOnServer.IsConnected);
            Assert.False(senderOnServer.IsConnected);

            HorseClient other = await Connect(port, "other");
            Assert.True(other.IsConnected);
        });
    }
}
