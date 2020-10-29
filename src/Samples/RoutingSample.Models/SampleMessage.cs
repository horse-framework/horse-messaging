using Twino.Client.TMQ.Annotations;
using Twino.Client.TMQ.Models;

namespace RoutingSample.Models
{
	[RouterName("SAMPLE-MESSAGE-ROUTER")]
	[QueueName("SAMPLE-MESSAGE-QUEUE")]
	[ContentType(1001)]
	[QueueStatus(MessagingQueueStatus.Push)]
	public class SampleMessage
	{
		public string Content { get; set; }
	}
}