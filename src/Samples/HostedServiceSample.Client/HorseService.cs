namespace HostedServiceSample.Client
{
	internal sealed class HorseService<T> : HorseServiceBase
		where T : class
	{
		public HorseService(string[] args) : base(args) { }
	}
}