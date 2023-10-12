using System;
using System.Text.Json;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;

namespace Horse.Messaging.Server.Helpers;

internal class SerializerFactory
{
    private static JsonSerializerOptions _options;

    internal static JsonSerializerOptions Default()
    {
        return _options ??= new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new EnumConverter<QueueAckDecision>(),
                new EnumConverter<RouteMethod>(),
                new EnumConverter<MessageTimeoutPolicy>(),
                new EnumConverter<PutBack>(),
                new EnumConverter<MessagingQueueType>(),
                new EnumConverter<QueueDestroy>(),
                new EnumConverter<QueueStatus>(),
                new EnumConverter<CommitWhen>(),
                new EnumConverter<ChannelStatus>(),
                new EnumConverter<PutBackDecision>(),
                new EnumConverter<MessageLimitExceededStrategy>(),
                new EnumConverter<QueueStatusAction>(),
                new EnumConverter<QueueType>()
            }
        };
    }

    internal static void AddConverter<T>(EnumConverter<T> converter) where T : struct, Enum
    {
        var options = Default();
        options.Converters.Add(converter);
    }
}