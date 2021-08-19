using AdvancedSample.Core.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedSample.DataAccess.Repository
{
	public static class ServiceExtensions
	{
		public static void AddUnitOfWork(this IServiceCollection services)
		{
			services.AddTransient<IUnitOfWork, UnitOfWork>();
		}
	}
}