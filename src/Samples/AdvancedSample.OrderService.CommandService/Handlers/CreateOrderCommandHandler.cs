using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Infrastructure.Handlers;
using AdvancedSample.OrderService.Core.BusinessManagers.Interfaces;
using AdvancedSample.OrderService.Models.Commands;
using AdvancedSample.OrderService.Models.DataTransferObjects;
using AutoMapper;
using Newtonsoft.Json;

namespace AdvancedSample.OrderService.CommandService.Handlers
{
	public class CreateOrderCommandHandler : CommandHandler<CreateOrderCommand>
	{
		private readonly IOrderBusinessManager _bm;
		private readonly IMapper _mapper;

		public CreateOrderCommandHandler(IOrderBusinessManager bm, IMapper mapper)
		{
			_bm = bm;
			_mapper = mapper;
		}

		protected override async Task Handle(CreateOrderCommand command)
		{
			_ = Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(command)}");

			OrderDTO order = _mapper.Map<OrderDTO>(command);
			await _bm.Create(order);
		}
	}
}