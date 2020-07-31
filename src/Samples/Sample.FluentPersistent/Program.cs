using System.Threading.Tasks;
using Twino.MQ;
using Twino.MQ.Data;
using Twino.MQ.Delivery;
using Twino.Server;

namespace Sample.FluentPersistent
{
    class Program
    {
        static async Task Main(string[] args)
        {
            /*
            server.UseTwinoMQ(mq => mq
.UseOptions(o => { })
                                      .AddPersistentQueues()
                                      .UseDeliveryHandler(dhBuilder =>
                                      {
                                          //dhBuilder.Channel
                                          //dhBuilder.Queue
                                          //dhBuilder.Headers
                                          //dhBuilder.CreatePersistentHandler()
                                      })

.UseAuthentication<IClientAuthenticator>()
.UseAuthorization<IClientAuthorization>()
.UseChannelAuthentication<IChannelAuthenticator>()
.UseAdminAuthorization<IAdminAuthorization>()
.UseChannelEventHandler<IChannelEventHandler>()
.UseClientHandler<IClientHandler>()
.UseServerMessageHandler<IServerMessageHandler>()
.UseClientIdGenerator<IUniqueIdGenerator>()
.UseMessageIdGenerator<IUniqueIdGenerator>()
                                      .LoadPersistentQueues());
*/

            TwinoMQ mq = TwinoMqBuilder.Create()
                                       .UseJustAllowDeliveryHandler()
                                       .AddPersistentQueues(cfg => cfg.KeepLastBackup())
                                       .Build();
            
            await mq.LoadPersistentQueues();
            await mq.CreatePersistentQueue("test", 0, DeleteWhen.AfterAcknowledgeReceived, ProducerAckDecision.AfterReceived);

            TwinoServer server = new TwinoServer();
            server.UseTwinoMQ(mq);
            server.Start(8000);
            await server.BlockWhileRunningAsync();
        }
    }
}