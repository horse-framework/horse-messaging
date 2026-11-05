using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;

namespace Horse.Messaging.Server.Transactions;

/// <summary>
/// Queue transaction endpoint pushes the transaction message to a queue.
/// The queue is created with default options if it does not exist.
/// </summary>
/// <remarks>
/// A new message is pushed for each transaction. Its message id is the transaction id,
/// it has the Transaction-Id header, and it carries the content and the custom headers
/// of the begin message. Headers that change queue behaviour are not carried.
/// Partition-Label is carried, so messages sent to a partitioned queue are routed by the client's label.
/// </remarks>
public class QueueTransactionEndpoint : IServerTransactionEndpoint
{
    private static readonly HashSet<string> ExcludedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        HorseHeaders.TRANSACTION_ID,
        HorseHeaders.CLIENT_ID,
        HorseHeaders.CLIENT_TOKEN,
        HorseHeaders.CLIENT_NAME,
        HorseHeaders.CLIENT_TYPE,
        HorseHeaders.QUEUE_MANAGER,
        HorseHeaders.QUEUE_TYPE,
        HorseHeaders.QUEUE_NAME,
        HorseHeaders.QUEUE_TOPIC,
        HorseHeaders.ACKNOWLEDGE,
        HorseHeaders.ACK_TIMEOUT,
        HorseHeaders.MESSAGE_TIMEOUT,
        HorseHeaders.CLIENT_LIMIT,
        HorseHeaders.DELAY_BETWEEN_MESSAGES,
        HorseHeaders.MESSAGE_ID_UNIQUE_CHECK,
        HorseHeaders.PUT_BACK,
        HorseHeaders.PUT_BACK_DELAY,
        HorseHeaders.DELIVERY,
        HorseHeaders.AUTO_DESTROY,
        HorseHeaders.MESSAGE_SIZE_LIMIT,
        HorseHeaders.CC,
        HorseHeaders.PARTITION_ID,
        HorseHeaders.PARTITION_LIMIT,
        HorseHeaders.PARTITION_SUBSCRIBERS
    };

    private readonly QueueRider _rider;
    private readonly string _queueName;
    private HorseQueue _queue;

    /// <summary>
    /// Queue name
    /// </summary>
    public string InitParameter => _queueName;

    /// <summary>
    /// Creates new queue transaction endpoint
    /// </summary>
    public QueueTransactionEndpoint(HorseQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
        _rider = queue.Rider.Queue;
        _queueName = queue.Name;
    }

    /// <summary>
    /// Creates new queue transaction endpoint.
    /// The queue does not need to exist, it is found or created when the first transaction is sent.
    /// </summary>
    public QueueTransactionEndpoint(QueueRider rider, string queueName)
    {
        ArgumentNullException.ThrowIfNull(rider);

        if (string.IsNullOrWhiteSpace(queueName))
            throw new ArgumentException("Queue name is required", nameof(queueName));

        _rider = rider;
        _queueName = queueName;
    }

    /// <summary>
    /// Pushes a message for the transaction into the target queue
    /// </summary>
    public async Task<bool> Send(ServerTransaction transaction)
    {
        HorseQueue queue = await GetQueue();
        if (queue == null)
            return false;

        PushResult result = await queue.PushTransactionMessage(CreateMessage(transaction));
        return result == PushResult.Success;
    }

    private async Task<HorseQueue> GetQueue()
    {
        HorseQueue queue = _queue;
        if (queue != null && !queue.IsDestroyed)
            return queue;

        queue = _rider.Find(_queueName) ?? await _rider.Create(_queueName, QueueOptions.CloneFrom(_rider.Options), null, false, true);

        _queue = queue;
        return queue;
    }

    private HorseMessage CreateMessage(ServerTransaction transaction)
    {
        HorseMessage source = transaction.Message;
        HorseMessage message = new HorseMessage(MessageType.QueueMessage, _queueName, source.ContentType);

        message.SetMessageId(transaction.Id);
        message.SetSource(source.Source);
        message.HighPriority = source.HighPriority;

        if (source.HasHeader && source.Headers != null)
        {
            foreach (KeyValuePair<string, string> header in source.Headers)
            {
                if (!ExcludedHeaders.Contains(header.Key))
                    message.AddHeader(header.Key, header.Value);
            }
        }

        message.AddHeader(HorseHeaders.TRANSACTION_ID, transaction.Id);

        if (source.Content != null && source.Content.Length > 0)
            message.Content = new MemoryStream(source.Content.ToArray());

        return message;
    }
}
