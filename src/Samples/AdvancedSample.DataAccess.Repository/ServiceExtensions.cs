using AdvancedSample.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedSample.DataAccess.Repository
{
	public static class ServiceExtensions
	{
		public static void AddRepositories<TEntity>(this IServiceCollection services)
			where TEntity : class, IEntity
		{
			services.AddTransient<ICommandRepository<TEntity>, Repository<TEntity>>();
			services.AddTransient<IQueryRepository<TEntity>, Repository<TEntity>>();
		}
	}
}