using System.Text.Json.Serialization;
using Horse.Messaging.Client.Direct.Annotations;
using Newtonsoft.Json;

namespace Sample.Consumer
{
	[DirectContentType(1000)]
	[DirectTarget(FindTargetBy.Type, "direct-handler")]
	public class RequestModel
	{
		[JsonProperty("id")]
		[JsonPropertyName("id")]
		public int Id { get; set; }
	}

	public class ResponseModel
	{
		[JsonProperty("no")]
		[JsonPropertyName("no")]
		public int No { get; set; }

		[JsonProperty("foo")]
		[JsonPropertyName("foo")]
		public string Foo { get; set; }
	}
}