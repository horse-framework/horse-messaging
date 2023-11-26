using System;
using System.Reflection;
using Horse.Messaging.Client.Channels.Annotations;
using Horse.Messaging.Client.Internal;

namespace Horse.Messaging.Client.Channels;

/// <summary>
/// Descriptor for channel subscriber and message models
/// </summary>
public class ChannelTypeResolver : ITypeDescriptorResolver<ChannelTypeDescriptor>
{
    private readonly HorseClient _client;

    /// <summary>
    /// Creates new channel type resolver
    /// </summary>
    public ChannelTypeResolver(HorseClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Resolves channel type
    /// </summary>
    public ChannelTypeDescriptor Resolve(Type type, ChannelTypeDescriptor defaultDescriptor)
    {
        ChannelTypeDescriptor descriptor = new ChannelTypeDescriptor();
        descriptor.Name = defaultDescriptor?.Name;

        if (_client.Channel.NameHandler != null && !IsSubscriberType(type))
        {
            string channelName = _client.Channel.NameHandler.Invoke(new ChannelNameHandlerContext
            {
                Type = type,
                Client = _client
            });

            if (!string.IsNullOrEmpty(channelName))
            {
                descriptor.Name = channelName;
                descriptor.ChannelNameSpecified = true;
            }
        }

        ChannelNameAttribute nameAttr = type.GetCustomAttribute<ChannelNameAttribute>();
        if (nameAttr != null)
            descriptor.Name = nameAttr.Name;

        InitialChannelMessageAttribute initMsgAttr = type.GetCustomAttribute<InitialChannelMessageAttribute>();
        if (initMsgAttr != null)
            descriptor.InitialChannelMessage = true;

        return descriptor;
    }


    private bool IsSubscriberType(Type type)
    {
        Type[] interfaceTypes = type.GetInterfaces();
        if (interfaceTypes.Length == 0)
            return false;

        Type openGenericType = typeof(IChannelSubscriber<>);

        foreach (Type interfaceType in interfaceTypes)
        {
            if (!interfaceType.IsGenericType)
                continue;

            Type[] genericArgs = interfaceType.GetGenericArguments();
            if (genericArgs.Length != 1)
                continue;

            Type genericType = openGenericType.MakeGenericType(genericArgs[0]);
            if (type.IsAssignableTo(genericType))
                return true;
        }

        return false;
    }
}