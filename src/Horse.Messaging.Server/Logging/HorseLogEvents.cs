namespace Horse.Messaging.Server.Logging;

internal static class HorseLogEvents
{
    internal const int QueueCreate = 201;
    internal const int QueueRemove = 202;
    internal const int QueueDeliveryProcess = 203;
    internal const int QueuePush = 204;
    internal const int QueuePull = 205;
    internal const int QueueCheckMessageTimeout = 206;
    internal const int QueuePullRequest = 207;
    internal const int QueueFill = 208;
    internal const int QueueInitialize = 209;
    internal const int QueueProcessMessage = 210;
    internal const int QueueApplyDecision = 211;
    internal const int QueueDelayedPutback = 212;
    internal const int QueueSaveMessage = 213;
    internal const int QueueAckReceived = 214;
    internal const int QueuePersistentDestroy = 215;

    internal const int ChannelCreate = 301;
    internal const int ChannelRemove = 302;
    internal const int ChannelPush = 303;

    internal const int RouterAdd = 401;
    internal const int RouterCreate = 402;
    internal const int RouterBindingSend = 403;
    internal const int RouterBindingAdd = 404;
    internal const int RouterBindingRemove = 405;
    internal const int RouterPublish = 406;
    internal const int AutoQueueBindingSend = 407;

    internal const int PluginLoadAssembly = 501;
    internal const int PluginAddAssembly = 502;
    internal const int PluginHandlerExecution = 503;

    internal const int SaveTransactionContainer = 601;

    internal const int LoadPersistentCache = 701;

    internal const int EventTrigger = 801;
}