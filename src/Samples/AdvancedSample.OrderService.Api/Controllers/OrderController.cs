using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Extensions;
using AdvancedSample.OrderService.Models.Commands;
using AdvancedSample.OrderService.Models.Queries;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace AdvanvedSample.OrderService.Api.Controllers
{
	[ApiController]
	[Route("api/order")]
	public class OrderController : Controller
	{
		private readonly IHorseRouterBus _bus;

		public OrderController(IHorseRouterBus bus)
		{
			_bus = bus;
		}

		[HttpGet("list")]
		public async Task<IActionResult> List()
		{
			GetAllOrderSnapshotsQuery query = new()
			{
				QueryId = Guid.NewGuid()
			};
			GetAllOrderSnapshotsQueryResult result = await _bus.ExecuteQuery<GetAllOrderSnapshotsQuery, GetAllOrderSnapshotsQueryResult>(query);
			return Ok(result);
		}

		[HttpGet("get/{id:int}")]
		public async Task<IActionResult> Get([FromRoute] int id)
		{
			GetOrderQuery query = new()
			{
				QueryId = Guid.NewGuid(),
				OrderId = id
			};
			GetOrderQueryResult result = await _bus.ExecuteQuery<GetOrderQuery, GetOrderQueryResult>(query);
			return Ok(result);
		}

		[HttpPost("create")]
		public async Task<IActionResult> Create([FromBody] CreateOrderCommand command)
		{
			command.CommandId = Guid.NewGuid();
			await _bus.ExecuteCommand(command);
			return Ok();
		}
	}
}