All Twino Features are listed below.


## Twino Server

* Lightweight and high speed TCP Server
* Fully multi-threaded server
* Supports all protocols with an interface implementation
* Supports multiple port and host bindings
* Supports SSL certificates
* Supports Content Encodings over all protocols: gzip, deflate and brotli
* Has ping, pong and keep alive managers for all implemented protocols

## Twino TMQ

* TMQ is a messaging protocol, you can read more about TMQ Protocol [here](https://github.com/mhelvacikoylu/twino/blob/v2/docs/TMQ%20Protocol.MD)
* TMQ Protocol message readers and writers
* TMQ Protocol Handlers
* Unique Id generators
* Has server side socket classes for using on TMQ protocol servers

## Twino MQ

* Twino MQ is a complete messaging queue server library and can be used with a few implementation
* Channel and content type based comminucation in queues
* Supports unlimited channels
* Each channel supports 65k queues
* Loading channels from JSON file
* Loading channels programmatically
* Adding and removing channels runtine
* Adding and removing queues runtime
* Client based authentication
* Channel based authentication
* Client based authorization; create channel and queue, send message to queue, send message to other client, pull from queue
* Extensible channel event management; queue created and removed, client joined and left, status changed
* Client name, token, type and other custom headers are supported

#### For Each Queue Features

* Sending to only first acquirer option
* Requesting acknowledge and sending automatically or manually options
* Acknowledge timeout option
* Messages, pending in queues, timeout option
* Using or not using unique message id
* Waiting acknowledge for previous message before send next message option
* Hiding or showing producer client unique id option
* Three type queuing with statuses route, push, pull
* Pausing and resuming queue options
* Stopping and restarting queues

#### For Each Message Features

* High priority messages
* Tracking message receivers
* Tracking message acknowledged
* Sending response message to a message
* Saving messages
* Message TTL option
* Advanced extensible messaging worklow with interface implementation
  * Decide what should to do when message is received
  * Decide what should to do when message is started to send to queue receivers
  * Decide for each single consumer if it can receive the message or not
  * After sending a message to a single consumer operations
  * After completed sending message operations to all consumers
  * Send completion decisions
  * Decide if message should be kept or removed
  * After message removed operations
  * Message acknowledge received operations
  * Message acknowledge timed out operations
  * Message queue timed out operations
  * Message delivery exception handling

* Multiple instances are supported can work disturbed
* For disturbed instances authentication and message routing options

## Twino HTTP

## Twino WebSockets

## Twino Serializable Models

## Twino MVC

## Twino Connectors

## Twino IOC

## Twino Data Extensions

## Twino HTTP Extensions
