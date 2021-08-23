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

	public class TestQueryResult
	{
		public string Bar { get; set; }
	}

	class Program
	{
		static async Task Main(string[] args)
		{
			HorseClientBuilder builder = new HorseClientBuilder();
			builder.SetHost("horse://localhost:15500");
			builder.UseNewtonsoftJsonSerializer();
			builder.SetResponseTimeout(TimeSpan.FromSeconds(16));
			HorseClient client = builder.Build();
			client.Connect();

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
			while (true)
			{
				// HorseResult result1 = await client.Router.PublishRequestJson<TestQuery, TestQueryResult>("test-service-route", query, 1);
				// Console.WriteLine($"Push: {result1.Code}");

				HorseResult result2 = await client.Router.PublishJson("test-service-route", command, null, true, 2);
				Console.WriteLine($"Push: {result2.Code}");

				// var result = await client.Direct.RequestJson<ResponseModel>(new RequestModel());
				// Console.WriteLine($"Push: {result.Code} ${JsonSerializer.Serialize(result.Model)}");
				Console.ReadLine();
				// await Task.Delay(5000);
			}
		}
	}
}