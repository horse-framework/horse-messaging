using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AdvancedSample.Common.Infrastructure.Handlers;
using AdvancedSample.DataAccess.Repository;
using AdvancedSample.OrderService.Domain;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using AdvancedSample.OrderService.Models.Queries;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AdvancedSample.OrderService.QueryService.Handlers
{
	public class GetOrderQueryHandler : QueryHandler<GetOrderQuery, GetOrderQueryResult>
	{
		private readonly IMapper _mapper;
		private readonly IUnitOfWork _uow;

		public GetOrderQueryHandler(IMapper mapper, IUnitOfWork uow)
		{
			_mapper = mapper;
			_uow = uow;
		}

		protected override async Task<GetOrderQueryResult> Handle(GetOrderQuery query)
		{
			_ = Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(query)}");

			OrderDTO item = await _uow.Query<Order>()
									  .Table
									  .ProjectTo<OrderDTO>(_mapper.ConfigurationProvider)
									  .FirstOrDefaultAsync(m => m.Id == query.OrderId);

			return new GetOrderQueryResult(item);
		}
	}
}