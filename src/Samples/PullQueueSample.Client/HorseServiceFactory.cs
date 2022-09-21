using System;
using Newtonsoft.Json;

namespace PullQueueSample.Client
{
	public static class HorseServiceFactory
	{
		public static IHorseService Create<T>(string[] args, string clientType) where T: class
		{
			Console.WriteLine(JsonConvert.SerializeObject(args, Formatting.Indented));
			return new HorseService<T>(args, clientType);
		}
	}
}