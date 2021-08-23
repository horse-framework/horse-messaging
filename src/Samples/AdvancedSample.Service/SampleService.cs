using AdvancedSample.Service.Interceptors;
using Horse.Messaging.Client;

namespace AdvancedSample.Service
{
	public sealed class SampleService<T> : SampleServiceBase
		where T : class
	{
		public SampleService(string clientType, string[] args) : base(clientType, args)
		{
			ConfigureHorseClient(ConfigureHorseClient);
		}

		private static void ConfigureHorseClient(HorseClientBuilder builder)
		{
			builder.AddScopedInterceptor<TestInterceptor>();
			builder.AddScopedConsumers(typeof(T));
			builder.AddScopedDirectHandlers(typeof(T));
		}
	}
}