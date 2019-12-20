## Creating Queues

Before discuss queue creation and it's options, we should talk about queues.
A queue is a series of same type messages in a channel.
There may be multiple queues in same channel.
When a client is joined to a channel, it receives all queue messages on this channel.
Clients do not join to queues, they join to channels. Queues are just for sorting and grouping same type of messages.

Let's talk about creation queues.
There are three ways of creating a queue

### Create Queues Programmatically in MQ Server

In server side, creating a queue programmatically is the easiest way.
In order to create a queue, first you should find the channel or create new channel.
Here are two examples; first is creating channel and queue, second is finding exist channel and creating a queue on it.

      sample code

### Create Queues With Server Builder

When you need to set channel and queue creations outside of your code. You can create new server build options file.
Then you can build server with ServerBuilder object.
This object loads all channels and queues with their options and initializes them.
This option is useful when your channels are queues always same and you have multiple instances.
You can copy your builder JSON file to all your instances and they can load same options, channels and queues from this file.

      sample code

### Create Queues From Clients Over Network Connection

Sometimes, queues should be created dynamically over network.
Producers decide which queues should be created and they need to send a queue creation message to server.
While sending this message, they can attach some options.
These options, if they are exists, override queue's server-side defined default options.

      sample code
