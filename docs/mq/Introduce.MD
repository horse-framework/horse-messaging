## Introducing to Twino Messaging Queue Server

Twino Messaging Queue Server is a messaging queue server library.
You can create full MQ Server with Twino MQ with a few interface implementations.
But alone, Twino MQ isn't an executable MQ server.

Twino Server uses TMQ protocol.
We will shortly discuess TMQ protocol in this article.
But you can read more about TMQ protocol at [here](https://github.com/mhelvacikoylu/twino/blob/v2/docs/mq/TMQ%20Protocol.MD)

TMQ Protocol is binary based protocol and can run over TCP protocol.
The objects of the TMQ Protocol are here:
* Each message has a message type. Tells us message type queue, peer to peer, acknowledge, response and more...
* Each message contains source information. Tells to receiver, who is sending this message.
* Each message contains target information. Tells to readers, who will receive this message.
* Each message has a content type. Describes content of the message.
* Each message has a unique id.

And there are some other properties such as high priority, first acquirer, is response or acknowledge required and time to live.
But the key is:

**What type** of message, contains **What**, is going to **Where** and sent by **Who**

Independently server or client, when you see a TMQ Protocol Message, you can have a good information what's going on.
Thats provides us these advantages:

* Everyone can send a message to every target
* Each receiver can know who is sent this message (you can hide it optionally)
  * So they can send response message
* With content type you can have information the message content and you can deserialize or process it directly.
* Target is not just a queue, target can be a client, a group of clients by name or type, server and more depends your logic.
* Each message can be tracked with acknowledge and responses because all messages can have unique id (or they can be anonymous)

Let's create a MQ Server and Client sample.
* Create new messaging queue server
* Create a channel and a queue in it
* Connect to the server
* Join to the channel
* Push a message to the queue
* Write everthing to console

Now we will write everything in delivery steps to console. So we need to implement IMessageDeliveryHandler

    public class SampleMessageDelivery : IMessageDeliveryHandler
    {
        public async Task<Decision> ReceivedFromProducer(ChannelQueue queue,
                                                         QueueMessage message,
                                                         MqClient sender)
        {
            Console.WriteLine($"A mesage is received: " + message.Message);
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<Decision> BeginSend(ChannelQueue queue,
                                              QueueMessage message)
        {
            Console.WriteLine("Message is about to send to consumers");
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task<bool> CanConsumerReceive(ChannelQueue queue,
                                                   QueueMessage message,
                                                   MqClient receiver)
        {
            Console.WriteLine($"Message is about to send to {receiver.Name}");
            return await Task.FromResult(true);
        }

        public async Task ConsumerReceived(ChannelQueue queue,
                                           MessageDelivery delivery,
                                           MqClient receiver)
        {
            Console.WriteLine($"Message is sent to {receiver.Name}");
            await Task.CompletedTask;
        }

        public async Task<Decision> EndSend(ChannelQueue queue,
                                            QueueMessage message)
        {
            Console.WriteLine($"Message send operation has completed");
            return await Task.FromResult(new Decision(true, false));
        }

        public async Task AcknowledgeReceived(ChannelQueue queue,
                                              TmqMessage acknowledgeMessage,
                                              MessageDelivery delivery)
        {
            Console.WriteLine($"Acknowledge is received: {delivery.Message.Message}");
            await Task.CompletedTask;
        }

        public async Task MessageTimedOut(ChannelQueue queue,
                                          QueueMessage message)
        {
            Console.WriteLine("There are no receivers, message time is out");
            await Task.CompletedTask;
        }

        public async Task AcknowledgeTimedOut(ChannelQueue queue, 
                                              MessageDelivery delivery)
        {
            Console.WriteLine("Queue was requesting ack but no acknowledge received for the message");
            await Task.CompletedTask;
        }

        public async Task MessageRemoved(ChannelQueue queue,
                                         QueueMessage message)
        {
            Console.WriteLine($"Message is removed: {message.Message}");
            await Task.CompletedTask;
        }

        public async Task ExceptionThrown(ChannelQueue queue,
                                          QueueMessage message,
                                          Exception exception)
        {
            Console.WriteLine($"An exception is thrown on delivery: {exception}");
            await Task.CompletedTask;
        }

        public async Task<bool> SaveMessage(ChannelQueue queue, 
                                            QueueMessage message)
        {
            Console.WriteLine("Message save method is called");
            return await Task.FromResult(false);
        }
    }
    
Then do something funny

    class Program
    {
        static void Main(string[] args)
        {
            CreateServer();
            
            Console.WriteLine("Press enter to connect to the MQ server");
            Console.ReadLine();

            ConnectAndPush();

            Console.ReadLine();
        }

        static void CreateServer()
        {
            //create messaging queue server
            SampleMessageDelivery delivery = new SampleMessageDelivery();
            MqServer mq = new MqServer();
            mq.SetDefaultDeliveryHandler(delivery);

            //create channel and queue
            Channel channel = mq.CreateChannel("ch");
            channel.CreateQueue(10).Wait();

            //create twino server, use mq and listen port 26222
            TwinoServer server = new TwinoServer();
            server.UseMqServer(mq);
            server.Start(26222);
        }

        static void ConnectAndPush()
        {
            TmqClient client = new TmqClient();
            client.SetClientName("demo-client");
            client.Connect("tmq://localhost:26222");
            client.MessageReceived += (c, msg) => Console.WriteLine($"Received from the queue: {msg}");

            bool joined = client.Join("ch", true).Result;
            Console.WriteLine($"Channel join: {joined}");

            client.Push("ch", 10, "Hello, World!", false);
        }
    }
