using System.Threading;
﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PartitionedSample.Server;

HostApplicationBuilder serverBuilder = Host.CreateApplicationBuilder();
serverBuilder.Services.AddHostedService<ServerHostedService>();
IHost server = serverBuilder.Build();
server.Run();