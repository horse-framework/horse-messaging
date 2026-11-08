using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Routing;

/// <summary>
/// Direct message binding.
/// Targets clients.
/// Binding receivers are received messages as DirectMessage.
/// </summary>
public class DirectBinding : Binding
{
    private DateTime _clientListUpdateTime;
    private MessagingClient[] _clients;
    private int _roundRobinIndex = -1;
    private Func<MessagingClient, bool> _clientFilter;

    /// <summary>
    /// Custom client filter.
    /// </summary>
    /// <param name="filter"></param>
    protected void SetClientFilter(Func<MessagingClient, bool> filter)
    {
        _clientFilter = filter;
    }

    /// <summary>
    /// Sends the message to binding receivers
    /// </summary>
    public override async Task<bool> Send(MessagingClient sender, HorseMessage message)
    {
        try
        {
            MessagingClient[] clients = GetClients();
            Func<MessagingClient, bool> filter = _clientFilter;
            if (filter is not null)
                clients = ApplyFilter(clients, filter);

            if (clients.Length == 0)
                return false;

            message.Type = MessageType.DirectMessage;
            message.SetTarget(Target);

            if (ContentType.HasValue)
                message.ContentType = ContentType.Value;

            message.WaitResponse = Interaction == BindingInteraction.Response;
            switch (RouteMethod)
            {
                case RouteMethod.OnlyFirst:
                    return await clients[0].SendAsync(message);

                case RouteMethod.Distribute:
                    bool atLeastOneSent = false;
                    foreach (MessagingClient client in clients)
                    {
                        bool sent = await client.SendAsync(message);
                        if (sent && !atLeastOneSent)
                            atLeastOneSent = true;
                    }

                    return atLeastOneSent;

                case RouteMethod.RoundRobin:
                    return await SendRoundRobin(clients, message);

                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            Router.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.RouterBindingSend, $"BindingSend Type:Direct, Router:{Router.Name}, Binding:{Name}", e);
            return false;
        }
    }

    private Task<bool> SendRoundRobin(MessagingClient[] clients, HorseMessage message)
    {
        // Lock free: the counter only grows (wrapping), the client is chosen by its remainder
        uint next = unchecked((uint)Interlocked.Increment(ref _roundRobinIndex));
        return clients[next % (uint)clients.Length].SendAsync(message);
    }

    /// <summary>
    /// Returns the clients the filter accepts.
    /// The filter is applied for each message, since a binding may set a filter that depends on the message.
    /// Allocates nothing when every client is accepted, and at most two arrays otherwise.
    /// </summary>
    private static MessagingClient[] ApplyFilter(MessagingClient[] clients, Func<MessagingClient, bool> filter)
    {
        MessagingClient[] accepted = null;
        int count = 0;

        for (int i = 0; i < clients.Length; i++)
        {
            bool accept = filter(clients[i]);

            if (accepted == null)
            {
                if (accept)
                    continue;

                // first rejected client: the clients before it are all accepted
                accepted = new MessagingClient[clients.Length - 1];
                Array.Copy(clients, accepted, i);
                count = i;
                continue;
            }

            if (accept)
                accepted[count++] = clients[i];
        }

        if (accepted == null)
            return clients;

        if (count == 0)
            return Array.Empty<MessagingClient>();

        if (count < accepted.Length)
            Array.Resize(ref accepted, count);

        return accepted;
    }

    /// <summary>
    /// Gets client from cache or reload
    /// </summary>
    protected MessagingClient[] GetClients()
    {
        //using cache to prevent performance hurt thousands of message per second situations
        //receivers are reloded in every second while messages are receiving
        if (DateTime.UtcNow - _clientListUpdateTime > TimeSpan.FromMilliseconds(1000))
        {
            MessagingClient[] clients;

            if (Target.StartsWith("@type:", StringComparison.OrdinalIgnoreCase))
            {
                var list = Router.Rider.Client.FindByType(Target.Substring(6));
                clients = list == null ? Array.Empty<MessagingClient>() : list.ToArray();
            }
            else if (Target.StartsWith("@name:", StringComparison.OrdinalIgnoreCase))
            {
                var list = Router.Rider.Client.FindClientByName(Target.Substring(6));
                clients = list == null ? Array.Empty<MessagingClient>() : list.ToArray();
            }
            else
            {
                MessagingClient client = Router.Rider.Client.Find(Target);
                clients = client == null ? Array.Empty<MessagingClient>() : new[] {client};
            }

            _clientListUpdateTime = DateTime.UtcNow;
            _clients = clients;
            return clients;
        }

        return _clients;
    }
}