## Create Server with Options using ServerBuilder

When you want to create a messaging queue server with Twino MQ. You need some options.
MQ Server runs on Twino Server, so you need to implement twino server options.
It has a few options and in many cases using default options are good.
Then you need Messaging Queue server options.
And you need default channel options.
You also need default queue options.
If you need different options on different queues, it's commonly possible, you need to setup this options programmaticaly.

ServerBuilder is not a solution for all options initializations above, but it helps you a lot.
You can setup server, channel, queue options in same JSON file. Also you create some channels and queues on initialization.

here is ServerBuilder JSON file structure.

	{
	  .. server options here,  
	  "Instances": [
	    {
	      .. an instance option
	    },
	    {
	      .. another instance option
	    }
	  ],  
	  "Channels": {    
	    "A-Channel-Name": {
	      .. channel options here,
	      "Queues": {
        	"123": {
	          .. queue options here with 123 content type
	        },
	        "599: {
	          .. other queue options here with 599 content type
	        }
	      }
	    },
	    "Other-Channel": {
	      .. other channel options
	    }
	  }  
	}
  
In this structure,
* Root node is server options.
* In **Channels** key, each node is a channel options.
* In **Queues** key, each node is a queue options.
* If you do not specify an option in a queue options, if defined it will use channel's option.
* If not, builder will search same option on root node.
* If could not found on root node, builder will use Twino MQ default value.

You can save your options in a JSON file, on a database such as Mongo, or whereever you want.
Suppose we saved our builder options in a JSON file with names **mq-builder.json**.
Here is a sample code that shows how you can load:

    //load from file
    ServerBuilder builder = new ServerBuilder();
    builder.LoadFromFile("mq-builder.json");
            
    //this is required
    builder.AddDefaultDeliveryHandler(new DeliveryHandler());
            
    //optional methods if you want to add
    builder.AddAuthenticator(new YourClientAuthenticator());
    builder.AddAuthorization(new YourAuthorization());
    builder.AddDefaultChannelHandler(new YourChannelHandler());
    builder.AddDefaultChannelAuthenticator(new YourChannelAuthenticator());
            
    //create server
    MqServer server = builder.CreateServer();
    
In builder JSON file, keys are same with properties in MqServerOptions, ChannelOptions and ChannelQueueOptions classes.
For queue status value, you should pass a string value:
"route", "push", "pull", "pause" (or "paused"), "stop" (or "stoped", or "stopped")
