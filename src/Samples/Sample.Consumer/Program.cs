using System;
using Horse.Messaging.Client;

namespace Sample.Consumer
{
	class Program
	{
		static void Main(string[] args)
		{
			HorseClientBuilder builder = new HorseClientBuilder();

			HorseClient client = builder.AddHost("horse://localhost:26222")
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