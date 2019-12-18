## Queue Statuses

In Twino MQ, each queue has it's own status.
There can be many channels in a server and a channel can have many queues in it.

Twino has 5 queue status, three of them tell us how messages will handle, two of them manages statement of the queue.

### Route
**Route is the default status.**
Route is the simples status of a queue.
When a producer pushes a message to the queue, message will directly sent to available consumers.
If there is no consumer, message will be lost.

Route status is designed for, if you are broadcasting a live data you do not need to keep the sent messages.
Such as exchanges, live scores. Especially if your old data is trash or should not be sent to consumers,
route status will be the best choice.

### Push
Push status works like route, but if there are no consumers, message will be kept until a new consumer join to the channel.
If another message is pushed to the queue, it will be queued until new consumer join.
Each message has it's own timeout duration.
If in this duration, still no consumers, message will be removed.

If you are pushing data from producer to consumers and consumers should receive data as soon as possible
-until here same with route status- but you do not want to lose any data, all messages in queue should be transffered,
push status will be the best choice.

### Pull
Pull status work like opposite of pushing.
Producer pushes a message to the queue, message is kept until timeout.
In this duration, if a consumer sends a request message to the queue,
message is sent to the consumer and removed from the queue.

In pull status, each message is sent to only one consumer.
Because when a consumer pulls the message, message will be removed from the queue.
Next consumer can pull next message in the queue.

Pull status is designed for, when there are many producers and one consumer.
There is a stack of objects, producers are feeding the stack and there is one processor application,
pulling the items from the queue and processing them.

### Pause
Pause status can be used for pausing all statuses.
When a queue is paused, producers can push new messages, they will keep until timeout.
But messages are not sent to consumers. Consumers can't pull new messages.

When queue status has changed back to route or push,
all messages are sent immediately to available consumers.
If there are no consumers, all messages are removed in route status,
but messages will be kept until timeout in push status. 

When queue status has changed back to pull, all messages will wait for new requests messages from consumers.
They will not sent before request messages are received.

### Stop
Stop status clears all messages in queue.
In stop status, producers can push and consumers can pull. Queue stays inactive.
