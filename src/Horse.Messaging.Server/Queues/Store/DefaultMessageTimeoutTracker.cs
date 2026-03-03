﻿using System;
using System.Collections.Generic;
using System.Threading;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server.Logging;

namespace Horse.Messaging.Server.Queues.Store;

/// <summary>
/// Default message timeout tracker.
/// That tracker calls Remove methods of message stores for timed out messages.
/// Uses a Timer backed by ThreadPool instead of a dedicated Thread per queue
/// to avoid OutOfMemoryException when many queues are created.
/// </summary>
public class DefaultMessageTimeoutTracker : IMessageTimeoutTracker
{
    /// <inheritdoc />
    public IQueueMessageStore Store { get; }

    private readonly HorseQueue _queue;
    private Timer _timer;

    /// <summary>
    /// Creates new default message timeout tracker
    /// </summary>
    public DefaultMessageTimeoutTracker(HorseQueue queue, IQueueMessageStore store)
    {
        Store = store;
        _queue = queue;
    }

    private void CheckTimeouts(object state)
    {
        try
        {
            if (_queue.Options.MessageTimeout.Policy == MessageTimeoutPolicy.NoTimeout || _queue.Options.MessageTimeout.MessageDuration == 0)
                return;

            QueueMessage message;
            do
            {
                message = Store.ReadFirst();

                if (message == null)
                    break;

                if (!message.Deadline.HasValue)
                    break;

                if (message.Deadline.Value > DateTime.UtcNow)
                    break;

                Store.Remove(message);

                _queue.Info.AddMessageTimeout();
                message.MarkAsTimedOut();

                _ = Store.Manager.OnMessageTimeout(message);

                foreach (IQueueMessageEventHandler handler in _queue.Rider.Queue.MessageHandlers.All())
                    _ = handler.MessageTimedOut(_queue, message);

                _queue.MessageTimeoutEvent.Trigger(new KeyValuePair<string, string>(HorseHeaders.MESSAGE_ID, message.Message.MessageId));

                message = Store.ReadFirst();
            } while (message != null && message.Deadline.HasValue);
        }
        catch (Exception e)
        {
            _queue.Rider.SendError(HorseLogLevel.Error, HorseLogEvents.QueueCheckMessageTimeout, "CheckMessageTimeout: " + _queue.Name, e);
        }
    }

    /// <inheritdoc />
    public void Start()
    {
        _timer = new Timer(CheckTimeouts, null, 1000, 1000);
    }

    /// <inheritdoc />
    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        _timer = null;
    }
}