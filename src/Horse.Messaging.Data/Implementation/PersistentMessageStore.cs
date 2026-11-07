﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Managers;
using Horse.Messaging.Server.Queues.Store;

namespace Horse.Messaging.Data.Implementation;

/// <summary>
/// Message store object for persistent queues
/// </summary>
public class PersistentMessageStore : LinkedMessageStore
{
    internal Database Database { get; }

    /// <summary>
    /// Creates new persistent message store
    /// </summary>
    public PersistentMessageStore(IHorseQueueManager manager, DatabaseOptions databaseOptions)
        : base(manager)
    {
        Database = new Database(databaseOptions);
    }

    /// <summary>
    /// Initializes the store, opens the database file.
    /// </summary>
    public Task<List<HorseMessage>> Initialize()
    {
        return Database.Open();
    }

    internal Task CloseAsync()
    {
        return Database.CloseWithoutShrink();
    }

    /// <summary>
    /// Returns the database file that holds the record of the message.
    /// It is the other store's file when the priority of the message changed after it was saved.
    /// </summary>
    private Database RecordDatabase(QueueMessage message)
    {
        if (message != null && Manager is PersistentQueueManager manager)
            return manager.GetRecordStore(message).Database;

        return Database;
    }

    /// <inheritdoc />
    public override bool Remove(string messageId)
    {
        RecordDatabase(Find(messageId)).Delete(messageId).GetAwaiter().GetResult();
        return base.Remove(messageId);
    }

    /// <inheritdoc />
    public override void Remove(HorseMessage message)
    {
        QueueMessage queueMessage = message.MessageId != null ? Find(message.MessageId) : null;
        if (queueMessage != null && queueMessage.Message != message)
            queueMessage = null;

        RecordDatabase(queueMessage).Delete(message.MessageId).GetAwaiter().GetResult();
        base.Remove(message);
    }

    /// <inheritdoc />
    public override void Remove(QueueMessage message)
    {
        RecordDatabase(message).Delete(message.Message.MessageId).GetAwaiter().GetResult();
        base.Remove(message);
    }

    /// <summary>
    /// Removes the message from disk and queue
    /// </summary>
    public async Task<bool> RemoveMessage(QueueMessage message)
    {
        await RecordDatabase(message).Delete(message.Message.MessageId);

        // A consumed message (deleted after its ACK) is not in the store. Checking first skips
        // the linear list scan that LinkedMessageStore falls back to on an index miss.
        if (message.IsInQueue)
            base.Remove(message);

        return true;
    }

    internal void RemoveFromOnlyMemory(QueueMessage message)
    {
        base.Remove(message);
    }

    /// <summary>
    /// Removes the message from disk and queue.
    /// Returns false if the message is not in this store. Its record in this file is deleted anyway.
    /// </summary>
    public async Task<bool> RemoveMessage(string messageId)
    {
        QueueMessage message = Find(messageId);
        await RecordDatabase(message).Delete(messageId);
        return base.Remove(messageId);
    }

    /// <inheritdoc />
    public override async Task Clear()
    {
        try
        {
            await Database.Clear();
        }
        finally
        {
            await base.Clear();
        }
    }

    /// <inheritdoc />
    public override async Task Destroy()
    {
        try
        {
            await Database.RemoveDatabase();
        }
        finally
        {
            await base.Destroy();
        }
    }
}
