using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Infrastructure.Handlers;
using AdvancedSample.ProductService.Models.Commands;
using Horse.Messaging.Client.Direct.Annotations;
using Newtonsoft.Json;

namespace AdvancedSample.ProductService.CommandService.Handlers
{
	[DirectContentType(1000)]
	public class CreateProductCommandHandler : CommandHandler<CreateProductCommand>
	{
		protected override Task Handle(CreateProductCommand model)
		{
			return Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(model)}");
		}
	}
}