using System;
using System.Text.Json;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client;

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
                new EnumConverter<MessagingQueueType>()
            }
        };
    }

    internal static void AddConverter<T>(EnumConverter<T> converter) where T : struct, Enum
    {
        var options = Default();
        options.Converters.Add(converter);
    }
}