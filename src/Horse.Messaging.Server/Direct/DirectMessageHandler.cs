using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Helpers;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Network;
using Horse.Messaging.Server.Security;

namespace Horse.Messaging.Server.Direct;

internal class DirectMessageHandler : INetworkMessageHandler
{
    #region Fields

    private const string NamePrefix = "@name:";
    private const string TypePrefix = "@type:";

    /// <summary>
    /// Messaging Queue Server
    /// </summary>
    private readonly HorseRider _rider;

    public DirectMessageHandler(HorseRider rider)
    {
        _rider = rider;
    }

    #endregion

    public Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
    {
        string target = message.Target;
        if (string.IsNullOrEmpty(target))
            return Task.CompletedTask;

        //most messages target a unique id, the first character is enough to skip the prefix checks
        if (target[0] == '@')
        {
            if (target.StartsWith(NamePrefix, StringComparison.Ordinal))
                return ProcessMultipleReceiverClientMessage(client, _rider.Client.FindClientByName(target.AsSpan(NamePrefix.Length)), message, fromNode);

            if (target.StartsWith(TypePrefix, StringComparison.Ordinal))
                return ProcessMultipleReceiverClientMessage(client, _rider.Client.FindByType(target.AsSpan(TypePrefix.Length)), message, fromNode);
        }

        return ProcessSingleReceiverClientMessage(client, message, fromNode);
    }

    /// <summary>
    /// Processes the client message which has multiple receivers (message by name or type).
    /// High priority messages are sent to the first receiver only, others are sent to all receivers.
    /// </summary>
    private async Task ProcessMultipleReceiverClientMessage(MessagingClient sender, MessagingClient[] receivers, HorseMessage message, bool fromNode)
    {
        if (receivers.Length == 0)
        {
            await OnReceiverNotFound(sender, message, fromNode);
            return;
        }

        int count = message.HighPriority ? 1 : receivers.Length;
        IClientAuthorization[] authorizations = sender != null ? _rider.Client.Authorizations.All() : [];
        IDirectMessageHandler[] handlers = _rider.Direct.MessageHandlers.All();
        List<MessagingClient> delivered = handlers.Length > 0 ? new List<MessagingClient>(count) : null;

        //serialized once for all receivers
        byte[] data = null;
        bool anyDelivered = false;

        for (int i = 0; i < count; i++)
        {
            MessagingClient receiver = receivers[i];

            if (authorizations.Length > 0 && !await CanDirectMessage(authorizations, sender, message, receiver))
                continue;

            data ??= HorseProtocolWriter.Create(message);

            anyDelivered = true;
            delivered?.Add(receiver);
            receiver.Stats.IncrementReceivedDirectMessages();
            await receiver.SendRawAsync(data);
        }

        if (!anyDelivered)
        {
            await OnUnauthorized(sender, message, fromNode);
            return;
        }

        if (sender != null)
            sender.Stats.SentDirectMessages++;

        if (delivered != null)
            RunOnDirect(handlers, sender, message, delivered);

        if (_rider.Direct.DirectEvent.HasSubscribers)
            _rider.Direct.DirectEvent.Trigger(sender, message.Target, new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.MessageId));
    }

    /// <summary>
    /// Processes the client message which has single receiver (message by unique id)
    /// </summary>
    private async Task ProcessSingleReceiverClientMessage(MessagingClient sender, HorseMessage message, bool fromNode)
    {
        MessagingClient receiver = _rider.Client.Find(message.Target);
        if (receiver == null)
        {
            await OnReceiverNotFound(sender, message, fromNode);
            return;
        }

        if (sender != null)
        {
            IClientAuthorization[] authorizations = _rider.Client.Authorizations.All();
            if (authorizations.Length > 0 && !await CanDirectMessage(authorizations, sender, message, receiver))
            {
                await OnUnauthorized(sender, message, fromNode);
                return;
            }
        }

        receiver.Stats.IncrementReceivedDirectMessages();
        await receiver.SendAsync(message);

        if (sender != null)
            sender.Stats.SentDirectMessages++;

        IDirectMessageHandler[] handlers = _rider.Direct.MessageHandlers.All();
        if (handlers.Length > 0)
            RunOnDirect(handlers, sender, message, [receiver]);

        if (_rider.Direct.DirectEvent.HasSubscribers)
            _rider.Direct.DirectEvent.Trigger(sender, message.Target, new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.MessageId));
    }

    private static async ValueTask<bool> CanDirectMessage(IClientAuthorization[] authorizations, MessagingClient sender, HorseMessage message, MessagingClient receiver)
    {
        foreach (IClientAuthorization authorization in authorizations)
            if (!await authorization.CanDirectMessage(sender, message, receiver))
                return false;

        return true;
    }

    /// <summary>
    /// Called when no receiver is found on this node.
    /// In Scaled cluster the message is relayed to the other nodes too, so a miss on one node does not mean the receiver does not exist.
    /// The node of the sender cannot know if another node has the receiver, and the other nodes must not answer over the node connection.
    /// In both cases nothing is sent and OnNotFound is not triggered.
    /// </summary>
    private async Task OnReceiverNotFound(MessagingClient sender, HorseMessage message, bool fromNode)
    {
        if (fromNode || IsRelayedToOtherNodes())
            return;

        if (message.WaitResponse && sender != null)
            await sender.SendAsync(message.CreateResponse(HorseResultCode.NotFound));

        foreach (IDirectMessageHandler handler in _rider.Direct.MessageHandlers.All())
        {
            try
            {
                HandlerFailureObserver.Observe(_rider, handler.OnNotFound(sender, message), HorseLogEvents.DirectMessageHandler, "IDirectMessageHandler.OnNotFound");
            }
            catch (Exception e)
            {
                HandlerFailureObserver.Report(_rider, e, HorseLogEvents.DirectMessageHandler, "IDirectMessageHandler.OnNotFound");
            }
        }
    }

    /// <summary>
    /// Called when the message could not be sent to any receiver because of authorization.
    /// Answers once and only if the sender waits for a response.
    /// </summary>
    private static Task OnUnauthorized(MessagingClient sender, HorseMessage message, bool fromNode)
    {
        if (fromNode || !message.WaitResponse || sender == null)
            return Task.CompletedTask;

        return sender.SendAsync(message.CreateResponse(HorseResultCode.Unauthorized));
    }

    /// <summary>
    /// Triggers OnDirect of the handlers without waiting, a failing handler does not affect the sender or the other handlers
    /// </summary>
    private void RunOnDirect(IDirectMessageHandler[] handlers, MessagingClient sender, HorseMessage message, List<MessagingClient> receivers)
    {
        foreach (IDirectMessageHandler handler in handlers)
        {
            try
            {
                HandlerFailureObserver.Observe(_rider, handler.OnDirect(sender, message, receivers), HorseLogEvents.DirectMessageHandler, "IDirectMessageHandler.OnDirect");
            }
            catch (Exception e)
            {
                HandlerFailureObserver.Report(_rider, e, HorseLogEvents.DirectMessageHandler, "IDirectMessageHandler.OnDirect");
            }
        }
    }

    private bool IsRelayedToOtherNodes()
    {
        ClusterManager cluster = _rider.Cluster;
        return cluster.Options.Mode == ClusterMode.Scaled && cluster.HasConnectedNode();
    }
}
