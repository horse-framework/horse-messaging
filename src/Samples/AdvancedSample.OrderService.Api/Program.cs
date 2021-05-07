using AdvancedSample.Core.Api;

CoreApi<Startup> api = new("order-api", 12002);
api.Run();

internal class Startup {}