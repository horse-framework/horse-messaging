using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Protocol.Events;

namespace Horse.Messaging.Client.Events;

/// <summary>
/// Event manager object for horse client
/// </summary>
public class EventOperator
{
    internal HorseClient Client { get; }

    internal List<EventSubscriberRegistration> Registrations { get; } = new();

    internal EventOperator(HorseClient client)
    {
        Client = client;
    }

    internal async Task OnEventMessage(HorseMessage message)
    {
        EventSubscriberRegistration reg = Registrations.FirstOrDefault(x => (ushort) x.Type == message.ContentType &&
                                                                            (string.IsNullOrEmpty(x.Target) || x.Target == message.Target));
        if (reg == null)
            return;

        try
        {
            HorseEvent horseEvent = (HorseEvent) Client.MessageSerializer.Deserialize(message, typeof(HorseEvent));
            await reg.Executer.Execute(Client, message, horseEvent, Client.ConsumeToken);
        }
        catch (Exception ex)
        {
            Client.OnException(ex, message);
        }
    }

    /// <summary>
    /// Subscribes to an event
    /// </summary>
    public Task<HorseResult> Subscribe(HorseEventType eventType, string target, CancellationToken cancellationToken)
    {
        return Subscribe(eventType, target, false, cancellationToken);
    }

    /// <summary>
    /// Subscribes to an event with response verification
    /// </summary>
    public Task<HorseResult> Subscribe(HorseEventType eventType, string target, bool verifyResponse, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Event;
        message.ContentType = Convert.ToUInt16(eventType);
        message.SetTarget(target);
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return Client.WaitResponse(message, verifyResponse, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from an event
    /// </summary>
    public Task<HorseResult> Unsubscribe(HorseEventType eventType, string target, CancellationToken cancellationToken)
    {
        return Unsubscribe(eventType, target, false, cancellationToken);
    }

    /// <summary>
    /// Unsubscribes from an event with response verification
    /// </summary>
    public Task<HorseResult> Unsubscribe(HorseEventType eventType, string target, bool verifyResponse, CancellationToken cancellationToken)
    {
        HorseMessage message = new HorseMessage();
        message.Type = MessageType.Event;
        message.ContentType = Convert.ToUInt16(eventType);
        message.SetTarget(target);
        message.AddHeader(HorseHeaders.SUBSCRIBE, "No");
        message.WaitResponse = verifyResponse;

        if (verifyResponse)
            message.SetMessageId(Client.UniqueIdGenerator.Create());

        return Client.WaitResponse(message, verifyResponse, cancellationToken);
    }
}