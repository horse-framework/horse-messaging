## Changing Queue Options

Changing queue options is possible from two locations; in messaging queue application, over network with a connected client.

### Changing Queue Options in Server

In server you can use **FindChannel** method of server to find the channel.
After that, you can find the queue with **FindQueue** method of the channel object.
ChannelQueue object has an **Options** property that you can change all options by settings properties of the options.

Only **Status** must be changed with **SetStatus** method of channel queue object.
The Status property of options object is used for initialization of the queue.
After queue is initialized (initialization is completed in constructor of the queue object),
status must be changed with **SetStatus** method,
because changing a status needs more operation than just changing a property value.

### Changing Queue Options Over Network

In some cases, producer client needs to change queue properties or status over network.
Changing queue options over network may require authorization (if defined).
Changing queue options includes all properties and status information too.
Only properties you sent will be changed.
Here is the sample code:

         TmqClient client = new TmqClient();
         await client.ConnectAsync("tmqs://localhost:443");
         bool changed = await client.SetQueueOptions("channel-name", 123, o =>
         {
             o.AcknowledgeTimeout = TimeSpan.FromSeconds(10);
             o.Status = MessagingQueueStatus.RoundRobin;
         });
         
**changed** variable always waits a response from the server.
If changed is true, you can be sure client has permission to change queue options and options are changed.
