using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnumsNET;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Helpers;

namespace Horse.Messaging.Server.Queues.Sync;

internal class QueueClusterNotifier
{
    private HorseQueue _queue;

    private ClusterManager _cluster;

    internal QueueClusterNotifier(HorseQueue queue, ClusterManager cluster)
    {
        _queue = queue;
        _cluster = cluster;
    }

    internal async Task<bool> SendMessagePush(HorseMessage message)
    {
        if (_cluster.State == NodeState.Single)
            return true;

        HorseMessage clone = message.Clone(true, true, message.MessageId);

        clone.Type = MessageType.Cluster;
        clone.ContentType = KnownContentTypes.NodePushQueueMessage;

        switch (_cluster.Options.Acknowledge)
        {
            case ReplicaAcknowledge.None:
                foreach (NodeClient client in _cluster.Clients)
                    _ = client.SendMessage(clone);

                return true;

            case ReplicaAcknowledge.OnlySuccessor:
            {
                if (_cluster.SuccessorNode == null)
                    return false;

                NodeClient successorClient = _cluster.Clients.FirstOrDefault(x => x.Info.Id == _cluster.SuccessorNode.Id);

                if (successorClient == null || !successorClient.IsConnected)
                    return false;

                bool ack = await successorClient.SendMessageAndWaitAck(clone);

                if (ack)
                {
                    foreach (NodeClient client in _cluster.Clients)
                        if (client != successorClient)
                            _ = client.SendMessage(clone);
                }

                return ack;
            }

            case ReplicaAcknowledge.AllNodes:
            {
                if (_cluster.SuccessorNode == null)
                    return false;

                NodeClient successorClient = _cluster.Clients.FirstOrDefault(x => x.Info.Id == _cluster.SuccessorNode.Id);

                if (successorClient == null || !successorClient.IsConnected)
                    return false;

                bool successorAck = await successorClient.SendMessageAndWaitAck(clone);
                if (!successorAck)
                    return false;

                List<Task<bool>> tasks = new List<Task<bool>>();

                foreach (NodeClient client in _cluster.Clients)
                {
                    if (!client.IsConnected || client == successorClient)
                        continue;

                    Task<bool> task = client.SendMessageAndWaitAck(clone);
                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
                return true;
            }

            default:
                return false;
        }
    }

    internal void SendPutBack(HorseMessage message, bool regular)
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        HorseMessage msg = new HorseMessage(MessageType.Cluster, _queue.Name, KnownContentTypes.NodePutBackQueueMessage);
        msg.SetMessageId(message.MessageId);
        msg.SetStringContent(regular ? "1" : "0");
        _cluster.SendMessage(msg);
    }

    internal void SendMessageRemoval(HorseMessage message)
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        HorseMessage msg = new HorseMessage(MessageType.Cluster, _queue.Name, KnownContentTypes.NodeRemoveQueueMessage);
        msg.SetMessageId(message.MessageId);
        _cluster.SendMessage(msg);
    }

    internal void SendMessagesClear()
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        HorseMessage msg = new HorseMessage(MessageType.Cluster, _queue.Name, KnownContentTypes.NodeClearQueueMessage);
        _cluster.SendMessage(msg);
    }

    internal void SendStateChanged()
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        HorseMessage msg = new HorseMessage(MessageType.Cluster, _queue.Name, KnownContentTypes.NodeQueueStateMessage);
        msg.SetStringContent(_queue.Status.AsString(EnumFormat.Description));
        _cluster.SendMessage(msg);
    }

    internal void SendCreated()
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        NodeQueueInfo info = CreateNodeQueueInfo();
        HorseMessage msg = new HorseMessage(MessageType.Cluster, _queue.Name, KnownContentTypes.CreateQueue);
        msg.SetStringContent(System.Text.Json.JsonSerializer.Serialize(info, SerializerFactory.Default()));
        _cluster.SendMessage(msg);
    }

    internal void SendQueueUpdated()
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        NodeQueueInfo info = CreateNodeQueueInfo();
        HorseMessage msg = new HorseMessage(MessageType.Cluster, _queue.Name, KnownContentTypes.UpdateQueue);
        msg.SetStringContent(System.Text.Json.JsonSerializer.Serialize(info, SerializerFactory.Default()));
        _cluster.SendMessage(msg);
    }

    internal void SendRemoved()
    {
        if (_cluster.Options.Mode != ClusterMode.Reliable || _cluster.State != NodeState.Main)
            return;

        HorseMessage msg = new HorseMessage(MessageType.Cluster, _queue.Name, KnownContentTypes.RemoveQueue);
        _cluster.SendMessage(msg);
    }

    internal NodeQueueInfo CreateNodeQueueInfo()
    {
        return new NodeQueueInfo
        {
            Name = _queue.Name,
            Topic = _queue.Topic,
            HandlerName = _queue.ManagerName,
            Initialized = _queue.Status != QueueStatus.NotInitialized,
            PutBackDelay = _queue.Options.PutBackDelay,
            MessageSizeLimit = _queue.Options.MessageSizeLimit,
            MessageLimit = _queue.Options.MessageLimit,
            LimitExceededStrategy = _queue.Options.LimitExceededStrategy.AsString(EnumFormat.Description),
            ClientLimit = _queue.Options.ClientLimit,
            MessageTimeout = new MessageTimeoutStrategyInfo(_queue.Options.MessageTimeout.MessageDuration, _queue.Options.MessageTimeout.Policy.AsString(EnumFormat.Description), _queue.Options.MessageTimeout.TargetName),
            AcknowledgeTimeout = Convert.ToInt32(_queue.Options.AcknowledgeTimeout.TotalMilliseconds),
            DelayBetweenMessages = _queue.Options.DelayBetweenMessages,
            Acknowledge = _queue.Options.Acknowledge.AsString(EnumFormat.Description),
            AutoDestroy = _queue.Options.AutoDestroy.AsString(EnumFormat.Description),
            QueueType = _queue.Options.Type.AsString(EnumFormat.Description),
            Headers = _queue.InitializationMessageHeaders?.Select(x => new NodeQueueHandlerHeader
            {
                Key = x.Key,
                Value = x.Value
            }).ToArray()
        };
    }
}