using Horse.Mq.Client.Annotations;
using Horse.Mq.Client.Models;
using Horse.Protocols.Hmq;

namespace RoutingSample.Models
{
	[RouterName("SAMPLE-MESSAGE-ROUTER")]
	[QueueName("SAMPLE-MESSAGE-QUEUE")] // FOR QUEUE PUSH
	[ContentType(1001)]                 // FOR DIRECT PUSH
	[QueueStatus(MessagingQueueStatus.Push)]
	[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
	public class SampleMessage
	{
		public string Content { get; set; }
	}
}