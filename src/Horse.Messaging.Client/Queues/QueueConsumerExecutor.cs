using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Horse.Messaging.Client.Interceptors;
using Horse.Messaging.Client.Internal;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Client.Queues.Exceptions;
using Horse.Messaging.Protocol;

namespace Horse.Messaging.Client.Queues;

internal class QueueConsumerExecutor<TModel> : ExecutorBase
{
    private readonly Type _consumerType;
    private readonly IQueueConsumer<TModel> _consumer;
    private readonly Func<IHandlerFactory> _consumerFactoryCreator;
    private QueueConsumerRegistration _registration;
    private MoveOnErrorAttribute _moveOnError;
    private InterceptorRunner _interceptorRunner;

    public QueueConsumerExecutor(Type consumerType, IQueueConsumer<TModel> consumer, Func<IHandlerFactory> consumerFactoryCreator)
    {
        _consumerType = consumerType;
        _consumer = consumer;
        _consumerFactoryCreator = consumerFactoryCreator;
    }

    public override void Resolve(object registration)
    {
        _registration = registration as QueueConsumerRegistration;
        _interceptorRunner = new InterceptorRunner(_registration!.InterceptorDescriptors);
        ResolveAttributes(_registration!.ConsumerType);
        ResolveQueueAttributes();
    }

    private void ResolveQueueAttributes()
    {
        _moveOnError = _consumerType.GetCustomAttribute<MoveOnErrorAttribute>();

        if (!SendPositiveResponse)
        {
            AutoAckAttribute ackAttribute = _consumerType.GetCustomAttribute<AutoAckAttribute>();
            SendPositiveResponse = ackAttribute != null;
        }

        if (!SendNegativeResponse)
        {
            AutoNackAttribute nackAttribute = _consumerType.GetCustomAttribute<AutoNackAttribute>();
            SendNegativeResponse = nackAttribute != null;
            NegativeReason = nackAttribute?.Reason ?? NegativeReason.None;
        }
    }

    public override async Task Execute(HorseClient client, HorseMessage message, object model,
        CancellationToken cancellationToken)
    {
        TModel t = (TModel) model;
        ProvidedHandler providedHandler = null;

        try
        {
            if (_consumer != null)
            {
                await Consume(_consumer, message, t, client, null, cancellationToken);
            }
            else if (_consumerFactoryCreator != null)
            {
                IHandlerFactory handlerFactory = _consumerFactoryCreator();
                providedHandler = handlerFactory.CreateHandler(_consumerType);
                IQueueConsumer<TModel> consumer = (IQueueConsumer<TModel>) providedHandler.Service;
                await Consume(consumer, message, t, client, handlerFactory, cancellationToken);
            }
            else
                throw new NullReferenceException("There is no consumer defined");

            if (SendPositiveResponse)
                await client.SendAck(message, cancellationToken);
        }
        catch (Exception e)
        {
            if (_moveOnError != null && !string.IsNullOrEmpty(_moveOnError.QueueName))
            {
                HorseMessage clone = message.Clone(true, true, client.UniqueIdGenerator.Create());
                clone.SetStringAdditionalContent(System.Text.Json.JsonSerializer.Serialize(ExceptionDescription.Create(e)));
                clone.Type = MessageType.QueueMessage;
                clone.SetTarget(_moveOnError.QueueName);

                var ack = await client.SendAsync(clone, true, cancellationToken);

                if (ack.Code == HorseResultCode.Ok)
                    await client.SendAck(message, cancellationToken);
                else if (SendNegativeResponse)
                    await SendNegativeAck(message, client, e, cancellationToken);
            }
            else if (SendNegativeResponse)
                await SendNegativeAck(message, client, e, cancellationToken);

            await SendExceptions(message, client, e);
        }
        finally
        {
            providedHandler?.Dispose();
        }
    }

    private async Task Consume(IQueueConsumer<TModel> consumer, HorseMessage message, TModel model,
        HorseClient client, IHandlerFactory handlerFactory, CancellationToken cancellationToken)
    {
        if (Retry == null)
        {
            await _interceptorRunner.RunBeforeInterceptors(message, client, cancellationToken);
            await consumer.Consume(message, model, client, cancellationToken);
            await _interceptorRunner.RunAfterInterceptors(message, client, cancellationToken);
            return;
        }

        int count = Retry.Count == 0 ? 100 : Retry.Count;
        for (int i = 0; i < count; i++)
        {
            try
            {
                await _interceptorRunner.RunBeforeInterceptors(message, client, handlerFactory, cancellationToken);
                await consumer.Consume(message, model, client, cancellationToken);
                await _interceptorRunner.RunAfterInterceptors(message, client, handlerFactory, cancellationToken);
                return;
            }
            catch (Exception e)
            {
                Type type = e.GetType();
                if (Retry.IgnoreExceptions is {Length: > 0})
                {
                    if (Retry.IgnoreExceptions.Any(x => x.IsAssignableFrom(type)))
                        throw;
                }

                if (Retry.DelayBetweenRetries > 0)
                    await Task.Delay(Retry.DelayBetweenRetries, cancellationToken);

                if (i == count - 1)
                    throw;
            }
        }
    }
}