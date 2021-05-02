using System;
using System.Threading.Tasks;
using AdvancedSample.ProductService.Models.Commands;
using Horse.Messaging.Client.Routers;
using Horse.Messaging.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace AdvanvedSample.ProductService.Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class ProductController : Controller
	{
		private readonly IHorseRouterBus _bus;

		public ProductController(IHorseRouterBus bus)
		{
			_bus = bus;
		}

		[HttpPost]
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