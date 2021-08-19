using AdvancedSample.Core.Api;

CoreApi<Startup> api = new("product-api", 12001);
api.Run();

internal class Startup {}