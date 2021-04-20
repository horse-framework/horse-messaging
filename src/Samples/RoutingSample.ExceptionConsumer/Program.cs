using System;
using Horse.Messaging.Client;
using Horse.Messaging.Client.Queues;

namespace RoutingSample.ExceptionConsumer
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			HorseClient client = new HorseClient();
			client.SetClientType("SAMPLE-EXCEPTION-CONSUMER");
			client.MessageSerializer = new NewtonsoftContentSerializer();

			QueueConsumerRegistrar registrar = new QueueConsumerRegistrar(client.Queue);
			registrar.RegisterConsumer<SampleExceptionConsumer>();
			
			client.Connected += (c) =>
			{
				Console.WriteLine("CONNECTED");
				_ = client.Queue.Subscribe("SAMPLE-EXCEPTION-QUEUE", false);
			};
			client.Disconnected += (c) => Console.WriteLine("DISCONNECTED");
			client.MessageReceived += (client, message) => Console.WriteLine("Direct message received");
			client.Connect("horse://localhost:15500");

			while (true)
				Console.ReadLine();
		}
	}
}