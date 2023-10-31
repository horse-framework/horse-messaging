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


using AdvancedSample.Server.Implementations.Client;
using AdvancedSample.Server.Implementations.Queue;
using AdvancedSample.Server.Implementations.Router;
using AdvancedSample.Server.Models;
using Horse.Messaging.Data;
using Horse.Messaging.Protocol;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Cache;
using Horse.Messaging.Server.Channels;
using Horse.Messaging.Server.Clients;
using Horse.Messaging.Server.Cluster;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Sync;
using Horse.Messaging.Server.Routing;
using Horse.Server;

namespace AdvancedSample.Server
{
    public class Worker : BackgroundService
    {
        public static  ILogger<Worker> _logger;
        private QueueConfig queueConfig = new QueueConfig();
        private ChennelsOptionsConfig channelConfig = new ChennelsOptionsConfig();
        private CacheConfig cacheConfig = new CacheConfig();
        private ClientsConfig clientsConfig = new ClientsConfig();
        private ClusterConfig clusterConfig = new ClusterConfig();
        private ServerConfig serverConfig = new ServerConfig();
        private List<RouterConfig> routerConfig = new List<RouterConfig>();
        private ConnectorOptionsConfig connectorConfig = new ConnectorOptionsConfig();
        private string serverName = string.Empty;
        private ClientAuthentication authentication = new ClientAuthentication();

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;

            Program.Configuration.Bind("HorseServerConfig:QueueConfig", queueConfig);
            Program.Configuration.Bind("HorseServerConfig:ClientsConfig", clientsConfig);
            Program.Configuration.Bind("HorseServerConfig:CacheConfig", cacheConfig);
            Program.Configuration.Bind("HorseServerConfig:ServerName", serverName);
            Program.Configuration.Bind("HorseServerConfig:Server", serverConfig);
            Program.Configuration.Bind("HorseServerConfig:Limitations", connectorConfig);
            Program.Configuration.Bind("HorseServerConfig:Routers", routerConfig);
            Program.Configuration.Bind("HorseServerConfig:ClusterConfiguration", clusterConfig);
            Program.Configuration.Bind("HorseServerConfig:ChannelsOptions", channelConfig);
            Program.Configuration.Bind("HorseServerConfig:Authentication", authentication);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            StartServer();

        }

        #region private methods
        private Action<HorseChannelConfigurator> SetChannelsConfig()
        {
            return cfg =>
            {
                cfg.Options.AutoChannelCreation = channelConfig.AutoChannelCreation;
                cfg.Options.AutoDestroy = channelConfig.AutoDestroy;
                cfg.Options.ClientLimit = channelConfig.ClientLimit;
                cfg.Options.MessageSizeLimit = channelConfig.MessageSizeLimit;
            };
        }
        private Action<HorseRouterConfigurator> SetRoutersConfig()
        {
            return cfg =>
            {
                cfg.MessageHandlers.Add(new CustomRouterHandler());
            };
        }
        private Action<HorseQueueConfigurator> SetQueueConfig()
        {
            return cfg =>
            {
                cfg.EventHandlers.Add(new CustomQueueEventHandler());
                cfg.MessageHandlers.Add(new CustomMessageEventHandler());

                cfg.UsePersistentQueues("Persistent", dataConfigurator =>
                {
                    var basPath = Path.Combine(Program.RootAddress, !string.IsNullOrWhiteSpace(queueConfig.Persistent.DataConfig.DefaultRelativeDataPath)
                        ? queueConfig.Persistent.DataConfig.DefaultRelativeDataPath
                        : "data/config.json");

                    dataConfigurator.SetPhysicalPath(queue => CustomSetDataPath(basPath, queue, queueConfig.Persistent.DataConfig.UseSeperateFolder));

                    if (queueConfig.Persistent.DataConfig.AutoShrink.Enabled)
                    {
                        var timeInterval = queueConfig.Persistent.DataConfig.AutoShrink.Interval;

                        dataConfigurator.SetAutoShrink(true, StringToTimeSpan(timeInterval));

                    }


                    dataConfigurator.KeepLastBackup(queueConfig.Persistent.DataConfig.KeepLastBackup);

                    //dataConfigurator.SetConfigFile(Path.Combine(Program.RootAddress, queueConfig.Persistent.DataConfig.ConfigRelativePath ?? "data"));

                    var autoFlashInterval = StringToTimeSpan(queueConfig.Persistent.DataConfig.AutoFlushInterval);

                    if (autoFlashInterval != TimeSpan.Zero)
                    {
                        dataConfigurator.UseAutoFlush(autoFlashInterval);
                    }
                    else
                    {
                        dataConfigurator.UseInstantFlush();
                    }

                }, queue =>
                {
                    queue.Options.AutoDestroy = queueConfig.Persistent.Options.QueueAutoDestroy;
                    queue.Options.AutoQueueCreation = queueConfig.Persistent.Options.AutoQueueCreation;
                    queue.Options.Acknowledge = queueConfig.Persistent.Options.Acknowledge;
                    queue.Options.DelayBetweenMessages = queueConfig.Persistent.Options.DelayBetweenMessages;
                    queue.Options.ClientLimit = queueConfig.Persistent.Options.ClientLimit;
                    queue.Options.CommitWhen = queueConfig.Persistent.Options.CommitWhen;
                    queue.Options.LimitExceededStrategy = queueConfig.Persistent.Options.LimitExceededStrategy;
                    queue.Options.MessageLimit = queueConfig.Persistent.Options.MessageLimit;
                    queue.Options.MessageSizeLimit = queueConfig.Persistent.Options.MessageSizeLimit;
                    queue.Options.PutBack = queueConfig.Persistent.Options.PutBackDecision;
                    queue.Options.PutBackDelay = queueConfig.Persistent.Options.PutBackDelay;
                    queue.Options.MessageTimeout = new() {MessageDuration = 5, Policy = MessageTimeoutPolicy.PushQueue};
                    queue.Options.AcknowledgeTimeout = StringToTimeSpan(queueConfig.Persistent.Options.AcknowledgeTimeout);
                });



            };
        }
        private Action<HorseCacheConfigurator> SetCacheConfig()
        {
            return cfg =>
            {
                cfg.Options.DefaultDuration = StringToTimeSpan(cacheConfig.DefaultDuration);
                cfg.Options.MaximumDuration = StringToTimeSpan(cacheConfig.MaximumDuration);
                cfg.Options.ValueMaxSize = cacheConfig.ValueMaxSize;
                cfg.Options.MaximumKeys = cacheConfig.MaximumKeys;
            };
        }
        private Action<HorseRiderOptions> SetHorseConnectorOptions()
        {
            return cfg =>
            {
                cfg.RouterLimit = connectorConfig.Routers;
                cfg.ClientLimit = connectorConfig.Clients;
                cfg.ChannelLimit = connectorConfig.Channels;
                cfg.QueueLimit = connectorConfig.Queues;
            };
        }
        private Action<HorseClientConfigurator> SetClientConfig()
        {
            return cfg =>
            {
                cfg.Handlers.Add(new ClientHandler(_logger));
                //    cfg.Authorizations.Add(new CustomClientAuthorizer());
                //   cfg.Authenticators.Add(new CustomClientAuthenticator(_logger, authentication.AsymmetricKey, clientsConfig.UseTokenValidation));
            };
        }
        private void StartServer()
        {
            try
            {
                HorseRider connector = HorseRiderBuilder.Create()

              .ConfigureQueues(SetQueueConfig())
              .ConfigureClients(SetClientConfig())
              .ConfigureCache(SetCacheConfig())
              .ConfigureOptions(SetHorseConnectorOptions())
              .ConfigureRouters(SetRoutersConfig())
              //.ConfigureDirect(SetDirectMessageConfig())
              .ConfigureChannels(SetChannelsConfig())
               //.UseClientIdGenerator<CustomClientIdGenerator>()
               //.UseMessageIdGenerator<CustomMessageIdGenerator>()

              .Build();


                if (routerConfig.Any())
                {
                    foreach (var router in routerConfig)
                    {
                        var addedRouter = connector.Router.Add(router.Name, router.Method);
                        addedRouter.IsEnabled = router.IsEnabled;

                        foreach (var binding in router.Bindings)
                        {
                            addedRouter.AddBinding(MapBindingFromConfig(binding));
                        }
                    }
                }

                connector = ConfigClusters(connector);

                HorseServer server = new HorseServer(SetServerConfig());

                server.Options.PingInterval = 10;

                server.UseRider(connector);

                _ = Task.Factory.StartNew(async () =>
                {
                    while (true)
                        try
                        {
                            await Task.Delay(5000);
                            foreach (HorseQueue queue in connector.Queue.Queues)
                            {
                                Console.WriteLine($"QUEUE {queue.Name} has {queue.Manager.MessageStore.Count()} Messages");
                                if (queue.Manager == null)
                                    continue;

                                if (queue.Manager.Synchronizer.Status == QueueSyncStatus.None)
                                    continue;

                                Console.WriteLine($"Queue {queue.Name} Sync Status is {queue.Manager.Synchronizer.Status}");
                            }
                        }
                        catch
                        {
                        }
                });


                _logger.LogInformation("HorseService started.");
                server.Run();




            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
            }
        }
        private ServerOptions SetServerConfig()
        {
            try
            {
                return new ServerOptions()
                {
                    Hosts = serverConfig.Hosts,
                    ContentEncoding = serverConfig.ContentEncoding,
                    MaximumPendingConnections = serverConfig.MaximumPendingConnections,
                    PingInterval = serverConfig.PingInterval,
                    RequestTimeout = serverConfig.RequestTimeout
                };
            }
            catch (Exception)
            {
                return ServerOptions.CreateDefault();
            }
        }
        private HorseRider ConfigClusters(HorseRider connector)
        {
            if (clusterConfig.Enabled)
            {
                connector.Cluster.Options.Mode = clusterConfig.Mode;
                connector.Cluster.Options.Acknowledge = clusterConfig.Acknowledge;
                connector.Cluster.Options.Name = clusterConfig.Name;
                connector.Cluster.Options.SharedSecret = clusterConfig.SharedSecret;
                connector.Cluster.Options.NodeHost = clusterConfig.NodeHost;
                connector.Cluster.Options.PublicHost = clusterConfig.PublicHost;

                clusterConfig.Nodes.ForEach(node =>
                {
                    connector.Cluster.Options.Nodes.Add(new NodeInfo
                    {
                        Name = node.Name,
                        Host = node.Host,
                        PublicHost = node.PublicHost
                    });
                });
            }

            return connector;
        }
        private static TimeSpan StringToTimeSpan(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new InvalidDataException();
            }

            try
            {
                var timeIndicator = input.LastOrDefault();
                var timeValue = Convert.ToUInt64(input.Substring(0, input.Length - 1));

                switch (timeIndicator)
                {
                    case 's':
                        return TimeSpan.FromSeconds(timeValue);

                    case 'm':
                        return TimeSpan.FromMinutes(timeValue);

                    case 'h':
                        return TimeSpan.FromHours(timeValue);

                    default:
                        throw new InvalidDataException();
                }
            }
            catch (Exception)
            {
                throw new InvalidDataException();
            }
        }
        private static string CustomSetDataPath(string basePath, HorseQueue queue, bool createInSeparateFolder)
        {
            try
            {
                basePath = createInSeparateFolder ? Path.Combine(basePath, queue.Name) : basePath;

                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                return basePath + "/" + queue.Name + ".tdb";
            }
            catch
            {
                return "data-" + queue.Name + ".tdb";
            }
        }
        private Binding MapBindingFromConfig(BindingConfig conf)
        {
            Binding bind;
            switch (conf.Type)
            {
                case nameof(QueueBinding):
                    bind = new QueueBinding();
                    break;

                case nameof(DirectBinding):
                    bind = new DirectBinding();
                    break;

                case nameof(AutoQueueBinding):
                    bind = new AutoQueueBinding();
                    break;

                case nameof(HttpBinding):
                    bind = new HttpBinding();
                    break;

                case nameof(TopicBinding):
                    bind = new TopicBinding();
                    break;

                default:
                    throw new InvalidDataException();
            }

            bind.RouteMethod = conf.RouteMethod;
            bind.Target = conf.Target;
            bind.ContentType = conf.ContentType;
            bind.Name = conf.Name;
            bind.Interaction = conf.Interaction;

            return bind;
        }
        #endregion
    }
}
