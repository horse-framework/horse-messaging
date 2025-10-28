using System;
using System.Linq;
using System.Threading.Tasks;
using Horse.Messaging.Plugins;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Logging;
using Horse.Messaging.Server.Network;

namespace Horse.Messaging.Server.Plugins;

internal class PluginMessageHandler : INetworkMessageHandler
{
    private readonly HorseRider _rider;

    public PluginMessageHandler(HorseRider rider)
    {
        _rider = rider;
    }

    public async Task Handle(MessagingClient client, HorseMessage message, bool fromNode)
    {
        HorsePlugin plugin = _rider.Plugin.Plugins.FirstOrDefault(x => string.Equals(x.Name, message.Target));

        if (plugin == null)
        {
            await client?.SendAsync(message.CreateResponse(HorseResultCode.NotFound))!;
            return;
        }

        IHorsePluginHandler handler = plugin.GetDefaultRequestHandler();

        if (handler == null)
        {
            await client?.SendAsync(message.CreateResponse(HorseResultCode.NotFound))!;
            return;
        }

        if (handler.ContentType != message.ContentType)
        {
            IHorsePluginHandler specifiedHandler = plugin.GetRequestHandler(message.ContentType);
            
            if (specifiedHandler != null)
                handler = specifiedHandler;
        }

        HorsePluginContext context = new HorsePluginContext(HorsePluginEvent.PluginMessage, plugin, _rider.Plugin, message);

        try
        {
            await handler.Execute(context);
        }
        catch (Exception e)
        {
            _rider.SendError(HorseLogLevel.Error, HorseLogEvents.PluginHandlerExecution, "PluginHandlerExecution Error: " + plugin.Name + ", MessageId: " + message.MessageId, e);
        }

        if (context.Response != null)
            await client.SendAsync(context.Response);
    }
}