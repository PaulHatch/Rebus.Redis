using System;
using System.Threading.Tasks;
using Rebus.Messages;
using Rebus.Pipeline;

namespace Rebus.Redis.Async;

/// <summary>
/// Step handler which allows replies to be sent via Redis pub/sub if they were called using the Redis async API. 
/// </summary>
internal class ReplyStepHandler : IOutgoingStep
{
    public async Task Process(OutgoingStepContext context, Func<Task> next)
    {
        var message = context.Load<TransportMessage>();
        var replyContext = message.GetReplyToContext();
        if (replyContext is not null)
        {
            await replyContext.RedisReplyAsync(message);
        }
        else
        {
            await next();
        }
    }
}