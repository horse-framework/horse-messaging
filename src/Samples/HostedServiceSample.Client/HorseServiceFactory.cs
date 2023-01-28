using System;
using System.Text.Json;

namespace HostedServiceSample.Client
{
	public static class HorseServiceFactory
	{
		public static IHorseService Create<T>(string[] args, string clientType) where T: class
		{
            JsonSerializerOptions _options = new JsonSerializerOptions()
            {
                WriteIndented = true
            };

			Console.WriteLine(JsonSerializer.Serialize(args, _options));
			return new HorseService<T>(args, clientType);
		}
	}
}