using System;
using System.Threading.Tasks;
using AdvancedSample.ProductService.Models.Commands;
using AdvancedSample.ProductService.Models.Queries;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace AdvanvedSample.ProductService.Api.Controllers
{
	[ApiController]
	[Route("api/product")]
	public class ProductController : Controller
	{
		private readonly IHorseRouterBus _bus;

		public ProductController(IHorseRouterBus bus)
		{
			_bus = bus;
		}

		[HttpGet("list")]
		public async Task<IActionResult> List()
		{
			GetAllProductsQuery query = new()
			{
				QueryId = Guid.NewGuid()
			};
			HorseResult<GetAllProductsQueryResult> result = await _bus.PublishRequestJson<GetAllProductsQuery, GetAllProductsQueryResult>(query);
			return result.Code switch
			{
				HorseResultCode.Ok => Ok(result.Model),
				_                  => Problem(result.Reason)
			};
		}

		[HttpPost("create")]
		public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
		{
			command.CommandId = Guid.NewGuid();
			HorseResult result = await _bus.PublishJson(command, true);
			return result.Code switch
			{
				HorseResultCode.Ok => Ok(),
				_                  => Problem(result.Reason)
			};
		}
	}
}