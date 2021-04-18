using Horse.Messaging.Client.Annotations;
using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Models;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Protocol;
using Horse.Mq.Client.Annotations;

namespace RoutingSample.Models
{
	[RouterName("SAMPLE-MESSAGE-ROUTER")]
	[QueueName("SAMPLE-MESSAGE-QUEUE")] // FOR QUEUE PUSH
	[DirectContentType(1001)]                 // FOR DIRECT PUSH
	[QueueStatus(MessagingQueueStatus.Push)]
	[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
	public class SampleMessage
	{
		public string Content { get; set; }
	}
}