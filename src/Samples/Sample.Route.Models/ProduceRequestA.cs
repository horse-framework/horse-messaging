using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;

namespace Sample.Route.Models
{
	[QueueId(1001)]
	[ChannelName("PRODUCER_CH")]
	[WaitForAcknowledge]
	[QueueStatus(MessagingQueueStatus.Push)]
	public class ProduceRequestA { }
}