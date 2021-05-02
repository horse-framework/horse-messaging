using AdvancedSample.Common.Cqrs.Infrastructure;
using Horse.Messaging.Client.Routers.Annotations;

namespace AdvancedSample.ProductService.Models.Commands
{
	public class CreateProductCommand : ServiceCommand
	{
		public string Name { get; set; }
		public decimal Price { get; set; }
	}
}