using System;
using AdvancedSample.Core.Domain;

namespace AdvancedSample.OutboxService.Domain
{
	public class OutboxMessage : Entity
	{
		public string Data { get; set; }
	}
}