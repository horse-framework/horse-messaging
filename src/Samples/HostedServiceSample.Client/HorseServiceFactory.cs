using System;
using System.Text.Json;
using Horse.Messaging.Protocol;

namespace HostedServiceSample.Client
{
	public static class HorseServiceFactory
	{
		public static IHorseService Create<T>(string[] args, string clientType) where T: class
		{
            JsonSerializerOptions _options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

			Console.WriteLine(JsonSerializer.Serialize(args, _options));
			return new HorseService<T>(args, clientType);
		}
	}
}