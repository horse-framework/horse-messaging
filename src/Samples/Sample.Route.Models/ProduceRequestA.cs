using Twino.Client.TMQ.Annotations;

namespace Sample.Route.Models
{
	[QueueId(1001)]
	[ChannelName("PRODUCER_CH")]
	public class ProduceRequestA { }
}