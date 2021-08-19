using System.Threading.Tasks;
using AdvancedSample.Core.Domain;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AdvancedSample.DataAccess.Repository
{
	public interface ICommandRepository<T> where T : class, IEntity
	{
		public ValueTask<EntityEntry<T>> AddAsync(T entity);
		public EntityEntry<T> Update(T entity);
		public EntityEntry<T> Delete(T entity);
		public Task<EntityEntry<T>> Delete(int id);
		public Task<EntityEntry<T>> DeletePermanently(int id);
		public EntityEntry<T> DeletePermanently(T entity);
	}
}