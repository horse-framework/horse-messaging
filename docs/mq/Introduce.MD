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

Let's create the simplest MQ Server:

