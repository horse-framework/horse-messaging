using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Infrastructure.Handlers;
using AdvancedSample.DataAccess.Repository;
using AdvancedSample.ProductService.Domain;
using AdvancedSample.ProductService.Models.DataTransferObjects;
using AdvancedSample.ProductService.Models.Queries;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AdvancedSample.ProductService.QueryService.Handlers
{
	public class GetAllProductsQueryHandler : QueryHandler<GetAllProductsQuery, GetAllProductsQueryResult>
	{
		private readonly IMapper _mapper;
		private readonly IUnitOfWork _uow;

		public GetAllProductsQueryHandler(IMapper mapper, IUnitOfWork uow)
		{
			_mapper = mapper;
			_uow = uow;
		}

		protected override async Task<GetAllProductsQueryResult> Handle(GetAllProductsQuery query)
		{
			_ = Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(query)}");

			var items = await _uow.Query<Product>()
								  .GetAll()
								  .ProjectTo<ProductDTO>(_mapper.ConfigurationProvider)
								  .ToListAsync();

			return new GetAllProductsQueryResult(items);
		}
	}
}