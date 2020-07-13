# What's Twino MQ?

Twino MQ is a Messaging Queue Library for .NET Core projects. It provides to create your own messaging queue server application with a few interface implementations. It's extendable and flexible, allows you to customize incredibly a lot of things in a messaging queue server. If you want to go further about Twino MQ Architecture, you can read [Twino MQ Documentation](https://github.com/mhelvacikoylu/twino/blob/v3/docs/twino-mq.pdf)

## NuGet Packages

**[Twino Messaging Queue Server](https://www.nuget.org/packages/Twino.MQ)**<br>
**[Twino Messaging Queue Client](https://www.nuget.org/packages/Twino.Client.TMQ)**<br>
**[Lightweight Database For Twino MQ Queues](https://www.nuget.org/packages/Twino.MQ.Data)**<br>
**[TMQ Protocol Library for Creating Custom Servers](https://www.nuget.org/packages/Twino.Protocols.TMQ)**


### Basic MQ Server Example

Basic MQ Server creation example

    class Program
    {
        static void Main(string[] args)
        {
            MqServerOptions options = new MqServerOptions();
            //set your options here
            
            MqServer mq = new MqServer(options);
            mq.SetDefaultDeliveryHandler(new YourCustomDeliveryHandler());
            TwinoServer twinoServer = new TwinoServer(ServerOptions.CreateDefault());
            twinoServer.UseMqServer(mq);
            twinoServer.Start();
            
            //optional
            _server.Server.BlockWhileRunning();
        }
    }

