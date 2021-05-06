namespace AdvancedSample.OrderService.Models.DataTransferObjects
{
	public class OrderDTO
	{
		public int Id { get; set; }
		public int ProductId { get; set; }
		public int Quantity { get; set; }
		public int Status { get; set; }
	}
}