using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    private PeriodicTimer _timer;
    private CancellationTokenSource _cts = new();

    /// <summary>
    /// Creates new default message timeout tracker
    /// </summary>
    public DefaultMessageTimeoutTracker(HorseQueue queue, IQueueMessageStore store)
    {
        Store = store;
        _queue = queue;
    }

    private async Task RunTimer()
    {
        while (await _timer.WaitForNextTickAsync(_cts.Token))
        {
            try
            {
                MessageTimeoutStrategy strategy = _queue.Options.MessageTimeout;

                if (strategy.Policy == MessageTimeoutPolicy.NoTimeout || strategy.MessageDuration == 0)
                    continue;

                QueueMessage message;
                do
                {
                    message = Store.ReadFirst();

                    if (message?.Deadline == null)
                        break;

                    if (message.Deadline.Value > DateTime.UtcNow)
                        break;

                    if (!string.IsNullOrEmpty(strategy.TargetName))
                    {
                        if (strategy.Policy == MessageTimeoutPolicy.PushQueue)
                        {
                            var queue = _queue.Rider.Queue.Find(strategy.TargetName);
                            await queue.Push(message.Message);
                        }
                        else if (strategy.Policy == MessageTimeoutPolicy.PublishRouter)
                        {
                            var router = _queue.Rider.Router.Find(strategy.TargetName);
                            await router.Publish(null, message.Message);
                        }
                    }

                    Store.Remove(message);

                    _queue.Info.AddMessageTimeout();
                    message.MarkAsTimedOut();

                    await Store.Manager.OnMessageTimeout(message);

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
    }

    /// <inheritdoc />
    public void Start()
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = RunTimer();
    }

    /// <inheritdoc />
    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _timer?.Dispose();
        _timer = null;
        _cts = null;
    }
}