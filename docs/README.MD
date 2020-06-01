## Twino Documentation Home Page
<br>

You can see [Twino Messaging Queue Documentation](https://github.com/mhelvacikoylu/twino/blob/v3/docs/twino-mq.pdf)<br>


## Twino 3.0 Changes

#### Routers and Bindings
* New message type is added for routers: Router.
* Routers can redirect messages to multiple queues and clients.
* Each router has a name and type. There are 3 type of routers:
  -	Distribute: Sends a message to all bindings
  -	Only First: Sends the message to only first binding if it's available.
  -	Round Robin: Sends messages to bindings in a load balance.
    	
* Twino MQ Server uses Routers with IRouter interface. You can implement your custom routers too.
* Routers can be enabled or disabled in runtime.
* Routers uses bindings with base Binding type. You can implement your custom binding too.
* Twino has two type of bindings as default Queue and Direct.
* Queue bindings targets a queue.
* Direct bindings target a client or multiple clients with same name or type. (you can use * for filtering)
* Each binding has priority so router can iterate them within this priority.
* Each binding can manipulate message content type and can redirect to it's receivers as different.
* Each binding has an interaction type: None, Acknowledge and Response.
* If a router has at least one binding with an interaction, message producer can be respond with that interaction. So it's possible sending acknowledge from a binding queue to producer or sending response message from a binding client to producer.     
#### Pull Status Changes
* Old pulling one message each type concept are redesigned.
* Pulling a message from a queue requires a PullRequest parameter.
* You can pull multiple messages within a request.
* Response model includes how many messages are included.
* You can call an action for each received message from queue.
* Messages are sent one by one until request process completed.
* You can enable wait for acknowledge option for pull queues too. With that option you need to send ack message in each message action.
* You can pull messages in FIFO or LIFO order.
* You can tell server to remove all left messages (or priority messages, or both) after the request operation completed.
* You can get how many messages are left in queue after each message is received.
* PullTimeout is added to TmqClient. It checks duration between two messages in pull request.

#### Events
* New message type is added for events: Event.
* Clients can subscribe to events and receive an information message when the event is triggered.
* Events are public by default. You can manage authority with IClientAuthorization implementation. A new method is added to the interface.
* In TmqClient object, events are added into the operators. You can subscribe to an event such as client.Channels.OnCreated(...);
* There are many events:
  -	Client Connected: Triggered when a client connects to the server.
  -	Client Disconnected: Triggered when a client disconnected from the server.
  -	Client Joined: Triggered when a client joins to a channel.
  -	Client Left: Triggered when a client leaves from a channel.
  -	Channel Created: Triggered when a channel is created.
  -	Channel Removed: Triggered when a channel is removed.
  -	Queue Created: Triggered when a queue is created.
  -	Queue Updated: Triggered when a queue is updated.
  -	Queue Removed: Triggered when a queue is removed.
  -	Message Produced: Triggered when a message is produced into a queue.

#### Other Changes
* Route status name is changed to Broadcast.
* Message types are changed Channel to QueueMessage, Client to DirectMessage.
* Twino 3.0 uses new version of protocol TMQP 2.0 which supports headers.
* Message process performance improvements.
* Negative acknowledge option is added to Decision.
* Keeping message in queue option is replaced with Put Back option which gives more options about putting the message back into the queue.
* All message sending operations return new TwinoResult model.
* TmqClient methods are moved into their Operator object properties.
* Message Reader name is changed to Message Consumer.
* Message Consumer object supports Direct Message with OnDirect method.
* Tag names are added to queues.
* Clear messages in a queue over network by client is added. It also has authorization implementation in IAdminAuthorization.
* AddMessage methods are added to queues. In server side, you can manually add message and trigger queue push operation.
* Decisions can be applied on connected node instances too with SendDecisionToNodes method.

#### HTTP and MVC Changes
* Unnecessary Task state machines are removed from HTTP, MVC and WebSocket libraries. Performance is improved about 3%.
* MVC now supports Task return types without async keyword.
* MVC now uses System.Text.Json as default. Serialization options property is added to TwinoMvc object.  
