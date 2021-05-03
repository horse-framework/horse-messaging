using AdvancedSample.Core.Service;

CoreService service = new("stock-query-handler");
service.Run();

internal class Startup { };