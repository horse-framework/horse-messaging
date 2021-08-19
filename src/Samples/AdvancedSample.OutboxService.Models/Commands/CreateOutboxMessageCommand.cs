using Newtonsoft.Json;

namespace AdvancedSample.OutboxService.Models
{
	public class CreateOutboxMessageCommand
	{
		public string Data { get; }

		public CreateOutboxMessageCommand(object data)
		{
			Data = JsonConvert.SerializeObject(data);
		}
	}
}