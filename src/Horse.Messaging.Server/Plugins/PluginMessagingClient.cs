using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Horse.Core;
using Horse.Messaging.Plugins;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;

namespace Horse.Messaging.Server.Plugins;

internal class PluginMessagingClient : IPluginMessagingClient
{
    private readonly MessagingClient _client;

    public string UniqueId => _client.UniqueId;
    public string Name => _client.Name;
    public string Type => _client.Type;
    public ConnectionData Data => _client.Data;
    public bool IsAuthenticated => _client.IsAuthenticated;
    public DateTime ConnectedDate => _client.ConnectedDate;
    public string RemoteHost => _client.RemoteHost;

    public PluginMessagingClient(MessagingClient client)
    {
        _client = client;
    }

    public IEnumerable<string> GetQueues()
    {
        return _client.GetQueues().Select(x => x.Queue.Name);
    }

    public IEnumerable<string> GetChannels()
    {
        return _client.GetChannels().Select(x => x.Channel.Name);
    }

    public Task<bool> SendMessage(HorseMessage message)
    {
        return _client.SendAsync(message);
    }
}