namespace HostedServiceSample.Client
{
	internal sealed class HorseService<T> : HorseServiceBase
		where T : class
	{
		public HorseService(string[] args) : base(args)
		{
			// You can use the below code to add your all consumers into the dependency injection container
			// ConfigureHorseClient(builder => builder.AddTransientConsumers(typeof(T)));
		}
	}
}