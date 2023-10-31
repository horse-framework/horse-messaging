//  --------------------------------------------------------------------
//  Copyright (c) 2005-2023 Arad ITC.
//
//  Author : Ammar heidari <ammar@arad-itc.org>
//  Licensed under the Apache License, Version 2.0 (the "License")
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0 
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  --------------------------------------------------------------------

using AdvancedSample.Server.Models;
using Serilog;
using ILogger = Serilog.ILogger;

namespace AdvancedSample.Server
{
    public class Program
    {
        public static string RootAddress;
        private static ILogger _serilogLogger;
        private static LogConfig logConfig = new LogConfig();
        public static IConfiguration Configuration { get; set; }

        public static void Main(string[] args)
        {
            RootAddress = AppDomain.CurrentDomain.BaseDirectory;
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    Configuration = hostContext.Configuration;
                    services.AddHostedService<Worker>();

                }).ConfigureLogging(logger =>
                {
                    Configuration.Bind("LogConfig", logConfig);

                    _serilogLogger = new LoggerConfiguration()
                        .WriteTo
                        .File(
                             Path.Combine(RootAddress, logConfig.LogFileAddressDirectory, logConfig.LogFileName),
                             rollingInterval: RollingInterval.Day,
                             fileSizeLimitBytes: logConfig.FileSizeLimit
                             )
                        .CreateLogger();

                    logger.AddSerilog(_serilogLogger);
                })
                .UseWindowsService();
    }
}
