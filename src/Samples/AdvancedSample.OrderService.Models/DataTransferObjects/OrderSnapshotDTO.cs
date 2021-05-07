namespace AdvancedSample.OrderService.Models.DataTransferObjects
{
	public class OrderSnapshotDTO
	{
		public int Id { get; set; }
		public int ProductId { get; set; }
		public string ProductName { get; set; }
		public int Quantity { get; set; }
		public decimal TotalPrice { get; set; }
	}
}