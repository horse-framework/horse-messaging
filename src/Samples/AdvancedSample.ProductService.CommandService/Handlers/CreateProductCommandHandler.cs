using System;
using System.Threading.Tasks;
using AdvancedSample.Common.Infrastructure.Handlers;
using AdvancedSample.ProductService.Core.BusinessManagers.Interfaces;
using AdvancedSample.ProductService.Models.Commands;
using AdvancedSample.ProductService.Models.DataTransferObjects;
using AutoMapper;
using Horse.Messaging.Client.Direct.Annotations;
using Newtonsoft.Json;

namespace AdvancedSample.ProductService.CommandService.Handlers
{
	[DirectContentType(1000)]
	public class CreateProductCommandHandler : CommandHandler<CreateProductCommand>
	{
		private readonly IProductBusinessManager _bm;
		private readonly IMapper _mapper;

		public CreateProductCommandHandler(IProductBusinessManager bm, IMapper mapper)
		{
			_bm = bm;
			_mapper = mapper;
		}

		protected override async Task Handle(CreateProductCommand command)
		{
			_ = Console.Out.WriteLineAsync($"[CONSUMED] {JsonConvert.SerializeObject(command)}");
			
			ProductDTO product = _mapper.Map<ProductDTO>(command);
			await _bm.Create(product);
		}
	}
}