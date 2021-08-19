using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Extensions;
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
			GetAllProductsQueryResult result = await _bus.ExecuteQuery<GetAllProductsQuery, GetAllProductsQueryResult>(query);
			return Ok(result);
		}

		[HttpPost("create")]
		public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
		{
			command.CommandId = Guid.NewGuid();
			await _bus.ExecuteCommand(command);
			return Ok();
		}
	}
}