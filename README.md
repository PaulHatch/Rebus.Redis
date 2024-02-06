# Rebus Redis

This library provides Redis support for Saga persistence, outbox, and async messaging.

This is specifically designed to implement scatter/gather commands, where we want to send a single logical command to
multiple services in parallel, then return data from one or more of those services to the caller.

There is [an existing async library for Rebus](https://github.com/rebus-org/Rebus.Async) which uses the normal Rebus
transport to send a reply, however it is marked experimental and the reasoning given for this is quite a reasonable one
that [durable messages are not suitable for the ephemeral state of an async request](https://github.com/rebus-org/Rebus.Async/issues/19#issuecomment-1243273692). A pending async await is by
nature ephemeral, using a persistent queue to send a reply is undesirable. In place of using the normal Rebus transport,
this library uses Redis pub/sub to send the reply, only currently subscribed listeners will receive the reply making it
well suited for our purposes.

Using async you can make a call like this on the client:
```csharp
var response = await bus.SendRequest<ReplyMessage>(request);
```

This will send the message to the server as normal, as well as add a task to the Redis pub/sub subscription. On the server
you can then do a simple like this:
```csharp
await bus.ReplyAsync(replyMessage);
```

There are also some additional methods to allow flexibility in cases like sagas where the handler is not ready to reply
until some further action is taken. Calling `GetReplyContext` in the context of a Redis async request will return a
context to allow you to send a reply at some later date. This context is just an identifier, it is safe to store and can
be added to a saga state, allowing a future message to reply to the original request.
```csharp
var replyContext = messageContext.GetReplyContext();
await replyContext.ReplyAsync(replyMessage);
```

In addition, the timeout for the caller is sent along with the request so that the recipient of a message can determine
how long the caller will be waiting for a response, which may be useful for cancelling a task or determining whether to
send a response to the caller.

```csharp
// in the client (the default timeout is 15 seconds if not specified)
var response = await bus.SendRequest<ReplyMessage>(request, timeout: TimeSpan.FromSeconds(30));
// on the handler
var timeout = messageContext.GetReplyTimeout();
```

## Async Configuration

To configure async messaging, you need to enable Redis and configure the async messaging. This can be done as follows:
```csharp
Configure.With(activationHandler)
    .Options(o =>
    {
        o.SetBusName("main");
        o.EnableRedis("localhost:6379", r => r.EnableAsync());
    })
    // ...
```

By default, both client and server mode will be active. This means that a listener will be started to listen for replies
from dispatched requests and that a step handler will be registered to redirect replies sent from a Redis request to the
Redis publish channel. If you only want to use async messaging in one direction, you can disable the other mode as follows:
```csharp
Configure.With(activationHandler)
    .Options(o =>
    {
        o.SetBusName("main");
        o.EnableRedis("localhost:6379", r => r.EnableAsync(AsyncMode.Client)); // or AsyncMode.Host
    })
    // ...
```

Typically only one service would use Redis async messaging, e.g. a client facing service. If however you need to send
replies via Redis from one service to another and if each one has it's own Redis instance, you can configure the replies
to be routed based on the sender address. This can be done as follows
```csharp
Configure.With(activationHandler)
    .Options(o =>
    {
        o.SetBusName("main");
        o.EnableRedis("main-redis:6379", r => r.EnableAsync()
            .RouteRepliesTo("other-service", "other-redis:6379"));
    })
    // ...
```
Note that this impacts only the reply routing, all other Redis components will use the main Redis connection configured
when calling `EnableRedis`.

## Sage and Subscription Storage and Outbox

This library also provides a Redis implementation of the saga and subscription storage and an outbox implementation
modeled after the Postgres implementation in Rebus. The outbox is implemented using Redis streams, sage data is
stored using Redis hashes, and subscriptions using Redis lists.

Basic configuration for the saga storage and outbox is as follows:
```csharp
using var activationHandler = new BuiltinHandlerActivator();
Configure.With(activationHandler)
    .Options(o =>
    {
        o.SetBusName("main");
        o.EnableRedis("localhost:6379", r => r.EnableAsync());
    })
    .Outbox(o => o.StoreInRedis())
    .Sagas(s => s.StoreInRedis())
    .Subscriptions(s => s.StoreInRedis());
```

This outbox only makes sense to use when the activity being performed is also stored in Redis, e.g. for sagas that use
Redis storage. Internally the outbox is implemented using Redis streams using the StackExchange Redis client. Currently
due to the way that commands are multiplexed, blocking stream reads are not supported, since this would block all Redis
operations being performed by the application, only polling mode is available. To ensure messages are received as with
as little latency as possible, an additional Redis connection is created for each sender dedicated to listening for the
outgoing messages, using the arbitrary execute command to perform a blocking read. This can be disabled by calling 
`options.EnableBlockingRead(false);` in the outbox configuration method.