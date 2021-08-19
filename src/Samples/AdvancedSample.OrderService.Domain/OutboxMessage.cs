using System;
using System.ComponentModel.DataAnnotations;
using AdvancedSample.Core.Domain;

namespace AdvancedSample.OrderService.Domain
{
	public enum OutboxMessageStatus
	{
		Waiting,
		Pending,
		Sent
	}
	public class OutboxMessage : EntityBase
	{
		public string Type { get; set; }
		public string MessageJSON { get; set; }
		public OutboxMessageStatus Status { get; set; }
	}
}