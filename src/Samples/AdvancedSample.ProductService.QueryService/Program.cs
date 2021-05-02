using AdvancedSample.Common.Infrastructure.Definitions;
using AdvancedSample.Core.Service;

CoreService service = new(ClientTypes.PRODUCT_QUERY_SERVICE);
service.Run();

internal class Startup { };