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
	public class GetProductQueryHandler : QueryHandler<GetProductQuery, GetProductQueryResult>
	{
		private readonly IMapper _mapper;
		private readonly IUnitOfWork _uow;

		public GetProductQueryHandler(IMapper mapper, IUnitOfWork uow)
		{
			_mapper = mapper;
			_uow = uow;
		}

		protected override async Task<GetProductQueryResult> Handle(GetProductQuery query)
		{
			_ = Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(query)}");

			ProductDTO item = await _uow.Query<Product>()
										.Table
										.ProjectTo<ProductDTO>(_mapper.ConfigurationProvider)
										.FirstOrDefaultAsync(m => m.Id == query.ProductId);

			return new GetProductQueryResult(item);
		}
	}
}