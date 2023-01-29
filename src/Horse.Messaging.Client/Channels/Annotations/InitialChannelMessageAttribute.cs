using System;

namespace Horse.Messaging.Client.Channels.Annotations;

/// <summary>
/// Activates initial message system for channels.
/// The last published message is kept in server and
/// each subscribed client receives the latest published message right after subscribed to the channel.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class InitialChannelMessageAttribute : Attribute
{
}