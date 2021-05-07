using AdvancedSample.DataAccess.Repository;
using AdvancedSample.OrderService.Context;
using AdvancedSample.OrderService.Core.BusinessManagers;
using AdvancedSample.OrderService.Core.BusinessManagers.Interfaces;
using AdvancedSample.OrderService.Core.Mappers;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdvancedSample.OrderService.Core
{
	public static class ServiceExtensions
	{
		public static void AddCoreServices(this IServiceCollection services)
		{
			services.AddMappers();
			services.AddUnitOfWork();
			services.AddDbContext<DbContext, OrderContext>(ServiceLifetime.Transient);
		}

		public static void AddBusinessManagers(this IServiceCollection services)
		{
			services.AddTransient<IOrderBusinessManager, OrderBusinessManager>();
		}

		private static void AddMappers(this IServiceCollection services)
		{
			MapperConfiguration mapperConfiguration = new(mc => { mc.AddMapperProfiles(); });
			IMapper mapper = mapperConfiguration.CreateMapper();
			services.AddSingleton(mapper);
		}

		private static void AddMapperProfiles(this IMapperConfigurationExpression cfg)
		{
			cfg.AddProfile<OrderMapperProfile>();
		}
	}
}