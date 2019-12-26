## Removing Channels and Queues

Removing queues or channels is possible from two locations;
in messaging queue application, over network with a connected client.

### Removing Channels In Server

When you remove a channel in a server, all queues will be stopped and removed in it. And all subscriptions will be released.

You can remove channel by name or channel object itself. Here is an example code:

         server.RemoveChannel("channel-name");

         Channel otherChannel = server.FindChannel("other-channel");
         server.RemoveChannel(otherChannel);


### Removing Queues In Server

When you remove a queue in a channel, all messages will be removed in the queue.
If your messages should be saved, you must save them before queue removed.
If you are removing queue with a code in server, you need to save your data right before removing queue.

In order to remove a queue, first you need to find it's channel in server. Here is an example code:

          Channel channel = server.FindChannel("channel-name");
          await channel.RemoveQueue(123);
          ChannelQueue queue = channel.FindQueue(124);
          await channel.RemoveQueue(queue);

### Remove Channel Over Network

Removing channel over network will remove all queues and all messages in the channel.
Removing a channel is very simple with TmqClient. Here is the sample code:

          bool removed = await client.RemoveChannel("channel-name", true);
          
Last parameter true if verification from server.
If you pass this value true, removed variable will be true if remove operation is verified by server.
If false, it will be true if message is successfuly sent over network.

### Remove Queue Over Network

Removing queue as simple as removing a channel with TmqClient.
Only change is second parameter is queue id.
Here is the sample code:

          bool removed = await client.RemoveQueue("channel-name", 123, true);
          
