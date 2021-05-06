using System.Threading.Tasks;
using AdvancedSample.OrderService.Domain;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.OrderService.Core.BusinessManagers.Interfaces
{
	public interface IOrderBusinessManager
	{
		public ValueTask<EntityEntry<Order>> Create(OrderDTO product);
	}
}