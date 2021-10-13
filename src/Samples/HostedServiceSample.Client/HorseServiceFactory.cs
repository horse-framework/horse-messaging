namespace HostedServiceSample.Client
{
	public static class HorseServiceFactory
	{
		public static IHorseService Create<T>(string[] args) where T : class
		{
			return new HorseService<T>(args);
		}
	}
}