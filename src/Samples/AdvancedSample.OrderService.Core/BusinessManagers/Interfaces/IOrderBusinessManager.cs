using System.Threading.Tasks;
using AdvancedSample.OrderService.Domain;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.OrderService.Core.BusinessManagers.Interfaces
{
	public interface IOrderBusinessManager
	{
		public ValueTask<Order> Create(OrderDTO order);
		public ValueTask<OrderSnapshot> CreateSnapshot(OrderDTO order, ProductDTO product);
	}
}