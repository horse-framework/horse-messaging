using System;
using System.Text.Json;
using System.Threading.Tasks;
using Horse.Messaging.Client;
using Horse.Messaging.Protocol;
using Sample.Consumer;

namespace Sample.Producer
{
	public class TestQuery
	{
		public string Foo { get; set; }
	}

	public class TestCommand
	{
		public string Foo { get; set; }
	}

	public class TestEvent
	{
		public string Foo { get; set; }
	}

	public class TestQueryResult
	{
		public string Bar { get; set; }
	}

	class Program
	{
		static async Task Main(string[] args)
		{
			HorseClientBuilder builder = new HorseClientBuilder();
			builder.SetHost("horse://localhost:9999");
			builder.UseNewtonsoftJsonSerializer();
			builder.SetResponseTimeout(TimeSpan.FromSeconds(300));
			HorseClient client = builder.Build();
			client.ResponseTimeout = TimeSpan.FromSeconds(300);
			await client.ConnectAsync();

			ModelC c = new ModelC();

			ModelA a = new ModelA();
			a.Foo = "foo";
			a.No = 123;

			TestQuery query = new()
			{
				Foo = "Foo"
			};
			TestCommand command = new()
			{
				Foo = "Foo"
			};
			TestEvent @event = new()
			{
				Foo = "Foo"
			};

			while (true)
			{
				// HorseResult result = await client.Router.PublishRequestJson<TestQuery, TestQueryResult>("test-service-route", query, 1);
				// Console.WriteLine($"Push: {result.Code}");

				// HorseResult result = await client.Router.PublishJson("test-service-route", command, null, true, 2);
				// Console.WriteLine($"Push: {result.Code}");

				// var result = await client.Direct.RequestJson<ResponseModel>(new RequestModel());
				// Console.WriteLine($"Push: {result.Code} ${JsonSerializer.Serialize(result.Model)}");

				var result = await client.Queue.PushJson("SampleTestEvent", new TestEvent(), true);
				
				Console.WriteLine($"Push: {result.Code} {result.Reason} ${JsonSerializer.Serialize(result)}");
				Console.ReadLine();
				// await Task.Delay(5000);
			}
		}
	}
}