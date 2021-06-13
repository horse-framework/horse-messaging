using System;
using Horse.Messaging.Client;

namespace Sample.Consumer
{
	class Program
	{
		static void Main(string[] args)
		{
			HorseClientBuilder builder = new HorseClientBuilder();

			HorseClient client = builder.SetHost("horse://localhost:9999")
										.AddSingletonConsumers(typeof(Program))
										.AddSingletonConsumers(typeof(Program))
										 /*
										.ConfigureModels(cfg => //cfg.UseQueueName(type => "Username1")
															cfg.UseConsumerAck()
															.AddMessageHeader("Sender-Client-Name", "MyName")
															.SetPutBackDelay(TimeSpan.FromSeconds(10)))*/
										.Build();

			client.Connect();

			while (true)
				Console.ReadLine();
		}
	}
}