using AdvancedSample.Messaging.Server;
using Horse.Messaging.Server;
using Horse.Messaging.Server.Handlers;
using Horse.Messaging.Server.Queues;
using Horse.Messaging.Server.Queues.Delivery;
using Horse.Server;
using Sample.Server;
using QueueEventHandler = AdvancedSample.Messaging.Server.QueueEventHandler;

HorseRider rider = HorseRiderBuilder.Create()
									.ConfigureQueues(cfg =>
													 {
														 cfg.Options.Type = QueueType.Push;
														 cfg.EventHandlers.Add(new QueueEventHandler());
														 cfg.UseAckDeliveryHandler(AcknowledgeWhen.AfterReceived, PutBackDecision.No);
													 })
									.ConfigureClients(cfg => { cfg.Handlers.Add(new ClientHandler()); })
									.Build();
rider.Router.ConfigureRoutes();

HorseServer server = new();
server.UseRider(rider);
server.Run(15500);