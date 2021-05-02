using AdvancedSample.Core.Service;

CoreService service = new("stock-command-handler");
service.Run();

internal class Startup { };