using Horse.Messaging.Client.Direct.Annotations;
using Horse.Messaging.Client.Queues;
using Horse.Messaging.Client.Queues.Annotations;
using Horse.Messaging.Client.Routers.Annotations;
using Horse.Messaging.Protocol;

namespace RoutingSample.Models
{
	[RouterName("SAMPLE-MESSAGE-ROUTER")]
	[QueueName("SAMPLE-MESSAGE-QUEUE")] // FOR QUEUE PUSH
	[DirectContentType(1001)]                 // FOR DIRECT PUSH
	[QueueType(MessagingQueueType.Push)]
	[Acknowledge(QueueAckDecision.WaitForAcknowledge)]
	public class SampleMessage
	{
		public string Content { get; set; }
	}
}