using System.Threading.Tasks;
using AdvancedSample.ProductService.Domain;
using AdvancedSample.ProductService.Models.DataTransferObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.ProductService.Core.BusinessManagers.Interfaces
{
	public interface IProductBusinessManager
	{
		public ValueTask<EntityEntry<Product>> Create(ProductDTO product);
	}
}