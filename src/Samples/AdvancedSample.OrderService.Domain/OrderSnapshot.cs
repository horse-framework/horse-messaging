using System;
using AdvancedSample.Core.Domain;

namespace AdvancedSample.OrderService.Domain
{
	public class OrderSnapshot : IEntity
	{
		public int Id { get; set; }
		public bool IsDeleted { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
		public DateTime DeletedAt { get; set; }
		public int ProductId { get; set; }
		public string ProductName { get; set; }
		public int Quantity { get; set; }
		public decimal TotalPrice { get; set; }
	}
}