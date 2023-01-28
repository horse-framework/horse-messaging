using System.Text.Json.Serialization;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Queues.Annotations;

namespace Sample.Consumer
{
	[HighPriorityMessage]
	[DirectContentType(1000)]
	[DirectTarget(FindTargetBy.Type, "direct-handler")]
	public class RequestModel
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }
	}

	public class ResponseModel
	{
		[JsonPropertyName("no")]
		public int No { get; set; }

		[JsonPropertyName("foo")]
		public string Foo { get; set; }
	}
}