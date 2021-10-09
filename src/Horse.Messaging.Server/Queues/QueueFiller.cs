using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Server.Queues
{
    /// <summary>
    /// Fills bulk data into a queue
    /// </summary>
    public class QueueFiller
    {
        private readonly HorseQueue _queue;

        /// <summary>
        /// Creates new queue filler
        /// </summary>
        public QueueFiller(HorseQueue queue)
        {
            _queue = queue;
        }

        /// <summary>
        /// Fills JSON object data to the queue
        /// </summary>
        public PushResult FillJson<T>(IEnumerable<T> items, bool createAsSaved, bool highPriority) where T : class
        {
            try
            {
                if (_queue.Status == QueueStatus.Paused || _queue.Status == QueueStatus.OnlyConsume)
                    return PushResult.StatusNotSupported;

                int max = _queue.Manager.PriorityMessageStore.Count() + _queue.Manager.MessageStore.Count() + items.Count();
                if (_queue.Options.MessageLimit > 0 && max > _queue.Options.MessageLimit)
                    return PushResult.LimitExceeded;

                foreach (T item in items)
                {
                    HorseMessage message = new HorseMessage(MessageType.QueueMessage, _queue.Name);
                    message.HighPriority = highPriority;
                    message.WaitResponse = _queue.Options.Acknowledge != QueueAckDecision.None;
                    message.SetMessageId(_queue.Rider.MessageIdGenerator.Create());

                    message.Serialize(item, _queue.Rider.MessageContentSerializer);
                    QueueMessage qm = new QueueMessage(message, createAsSaved);


                    if (message.HighPriority)
                        _queue.Manager.PriorityMessageStore.Put(qm);
                    else
                        _queue.Manager.MessageStore.Put(qm);
                }

                _ = _queue.Trigger();

                return PushResult.Success;
            }
            catch (Exception e)
            {
                _queue.Rider.SendError("FILL_JSON", e, $"QueueName:{_queue.Name}");
                return PushResult.Error;
            }
        }

        /// <summary>
        /// Fills JSON object data to the queue.
        /// Creates new HorseMessage and before writing content and adding into queue calls the action.
        /// </summary>
        public PushResult FillJson<T>(IEnumerable<T> items, bool createAsSaved, Action<HorseMessage, T> action) where T : class
        {
            try
            {
                if (_queue.Status == QueueStatus.Paused || _queue.Status == QueueStatus.OnlyConsume)
                    return PushResult.StatusNotSupported;

                int max = _queue.Manager.PriorityMessageStore.Count() + _queue.Manager.MessageStore.Count() + items.Count();
                if (_queue.Options.MessageLimit > 0 && max > _queue.Options.MessageLimit)
                    return PushResult.LimitExceeded;

                foreach (T item in items)
                {
                    HorseMessage message = new HorseMessage(MessageType.QueueMessage, _queue.Name);
                    message.WaitResponse = _queue.Options.Acknowledge != QueueAckDecision.None;
                    message.SetMessageId(_queue.Rider.MessageIdGenerator.Create());

                    action(message, item);
                    message.Serialize(item, _queue.Rider.MessageContentSerializer);

                    QueueMessage qm = new QueueMessage(message, createAsSaved);

                    if (message.HighPriority)
                        _queue.Manager.PriorityMessageStore.Put(qm);
                    else
                        _queue.Manager.MessageStore.Put(qm);
                }

                _ = _queue.Trigger();

                return PushResult.Success;
            }
            catch (Exception e)
            {
                _queue.Rider.SendError("FILL_JSON", e, $"QueueName:{_queue.Name}");
                return PushResult.Error;
            }
        }

        /// <summary>
        /// Fills string data to the queue
        /// </summary>
        public PushResult FillString(IEnumerable<string> items, bool createAsSaved, bool highPriority)
        {
            try
            {
                if (_queue.Status == QueueStatus.Paused || _queue.Status == QueueStatus.OnlyConsume)
                    return PushResult.StatusNotSupported;

                int max = _queue.Manager.PriorityMessageStore.Count() + _queue.Manager.MessageStore.Count() + items.Count();
                if (_queue.Options.MessageLimit > 0 && max > _queue.Options.MessageLimit)
                    return PushResult.LimitExceeded;

                foreach (string item in items)
                {
                    HorseMessage message = new HorseMessage(MessageType.QueueMessage, _queue.Name);
                    message.HighPriority = highPriority;
                    message.WaitResponse = _queue.Options.Acknowledge != QueueAckDecision.None;
                    message.SetMessageId(_queue.Rider.MessageIdGenerator.Create());

                    message.Content = new MemoryStream(Encoding.UTF8.GetBytes(item));
                    message.Content.Position = 0;
                    message.CalculateLengths();

                    QueueMessage qm = new QueueMessage(message, createAsSaved);

                    if (message.HighPriority)
                        _queue.Manager.PriorityMessageStore.Put(qm);
                    else
                        _queue.Manager.MessageStore.Put(qm);
                }

                _ = _queue.Trigger();

                return PushResult.Success;
            }
            catch (Exception e)
            {
                _queue.Rider.SendError("FILL_STRING", e, $"QueueName:{_queue.Name}");
                return PushResult.Error;
            }
        }

        /// <summary>
        /// Fills binary data to the queue
        /// </summary>
        public PushResult FillData(IEnumerable<byte[]> items, bool createAsSaved, bool highPriority)
        {
            try
            {
                if (_queue.Status == QueueStatus.Paused || _queue.Status == QueueStatus.OnlyConsume)
                    return PushResult.StatusNotSupported;

                int max = _queue.Manager.PriorityMessageStore.Count() + _queue.Manager.MessageStore.Count() + items.Count();
                if (_queue.Options.MessageLimit > 0 && max > _queue.Options.MessageLimit)
                    return PushResult.LimitExceeded;

                foreach (byte[] item in items)
                {
                    HorseMessage message = new HorseMessage(MessageType.QueueMessage, _queue.Name);
                    message.HighPriority = highPriority;
                    message.WaitResponse = _queue.Options.Acknowledge != QueueAckDecision.None;
                    message.SetMessageId(_queue.Rider.MessageIdGenerator.Create());

                    message.Content = new MemoryStream(item);
                    message.Content.Position = 0;
                    message.CalculateLengths();

                    QueueMessage qm = new QueueMessage(message, createAsSaved);

                    if (message.HighPriority)
                        _queue.Manager.PriorityMessageStore.Put(qm);
                    else
                        _queue.Manager.MessageStore.Put(qm);
                }

                _ = _queue.Trigger();

                return PushResult.Success;
            }
            catch (Exception e)
            {
                _queue.Rider.SendError("FILL_DATA", e, $"QueueName:{_queue.Name}");
                return PushResult.Error;
            }
        }

        /// <summary>
        /// Fills Horse Message objects to the queue
        /// </summary>
        public PushResult FillMessage(IEnumerable<HorseMessage> messages, bool isSaved, Action<QueueMessage> actionPerMessage = null)
        {
            try
            {
                if (_queue.Status == QueueStatus.Paused || _queue.Status == QueueStatus.OnlyConsume)
                    return PushResult.StatusNotSupported;

                int max = _queue.Manager.PriorityMessageStore.Count() + _queue.Manager.MessageStore.Count() + messages.Count();
                if (_queue.Options.MessageLimit > 0 && max > _queue.Options.MessageLimit)
                    return PushResult.LimitExceeded;

                foreach (HorseMessage message in messages)
                {
                    message.SetTarget(_queue.Name);

                    if (string.IsNullOrEmpty(message.MessageId))
                        message.SetMessageId(_queue.Rider.MessageIdGenerator.Create());

                    message.CalculateLengths();

                    QueueMessage qm = new QueueMessage(message, isSaved);

                    if (message.HighPriority)
                        _queue.Manager.PriorityMessageStore.Put(qm);
                    else
                        _queue.Manager.MessageStore.Put(qm);

                    if (actionPerMessage != null)
                        actionPerMessage(qm);
                }

                _ = _queue.Trigger();

                return PushResult.Success;
            }
            catch (Exception e)
            {
                _queue.Rider.SendError("FILL_MESSAGE", e, $"QueueName:{_queue.Name}");
                return PushResult.Error;
            }
        }
    }
}