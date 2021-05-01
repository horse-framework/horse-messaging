using Horse.Messaging.Client.Events;

namespace AdvancedSample.Core.Service
{
	public interface IRegistrar
	{
		public void AddTransientConsumers();
		public void AddTransientSubscribers();
		public void AddTransientDirectHandlers();
		public void AddTransientHorseEvents();
		public void AddScopedConsumers();
		public void AddScopedSubscribers();
		public void AddScopedDirectHandlers();
		public void AddScopedHorseEvents();
		public void AddSingletonConsumers();
		public void AddSingletonSubscribers();
		public void AddSingletonDirectHandlers();
		public void AddSingletonHorseEvents();
		public void AddTransientConsumer<TConsumer>() where TConsumer : class;
		public void AddTransientChannelSubscriber<TChannelSubscriber>() where TChannelSubscriber : class;
		public void AddTransientDirectHandler<TDirectHandler>() where TDirectHandler : class;
		public void AddTransientHorseEvent<THorseEvent>() where THorseEvent : IHorseEventHandler;
		public void AddScopedConsumer<TConsumer>() where TConsumer : class;
		public void AddScopedChannelSubscriber<TChannelSubscriber>() where TChannelSubscriber : class;
		public void AddScopedDirectHandler<TDirectHandler>() where TDirectHandler : class;
		public void AddScopedHorseEvent<THorseEvent>() where THorseEvent : IHorseEventHandler;
		public void AddSingletonConsumer<TConsumer>() where TConsumer : class;
		public void AddSingletonChannelSubscriber<TChannelSubscriber>() where TChannelSubscriber : class;
		public void AddSingletonDirectHandler<TDirectHandler>() where TDirectHandler : class;
		public void AddSingletonHorseEvent<THorseEvent>() where THorseEvent : IHorseEventHandler;
	}
}