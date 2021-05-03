using AdvancedSample.Core.Domain;

namespace AdvancedSample.ProductService.Domain
{
	public class Product : Entity
	{
		public string Name { get; set; }
		public decimal Price { get; set; }
	}
}