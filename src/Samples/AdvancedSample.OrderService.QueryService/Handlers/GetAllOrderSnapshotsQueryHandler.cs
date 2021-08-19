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
	public class GetAllOrderSnapshotsQueryHandler : QueryHandler<GetAllOrderSnapshotsQuery, GetAllOrderSnapshotsQueryResult>
	{
		private readonly IMapper _mapper;
		private readonly IUnitOfWork _uow;

		public GetAllOrderSnapshotsQueryHandler(IMapper mapper, IUnitOfWork uow)
		{
			_mapper = mapper;
			_uow = uow;
		}

		protected override async Task<GetAllOrderSnapshotsQueryResult> Handle(GetAllOrderSnapshotsQuery query)
		{
			_ = Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(query)}");

			List<OrderSnapshotDTO> items = await _uow.Query<OrderSnapshot>()
													 .GetAll()
													 .ProjectTo<OrderSnapshotDTO>(_mapper.ConfigurationProvider)
													 .ToListAsync();

			return new GetAllOrderSnapshotsQueryResult(items);
		}
	}
}