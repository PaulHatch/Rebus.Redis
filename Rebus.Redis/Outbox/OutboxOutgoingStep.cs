using System;
using System.Threading.Tasks;
using Rebus.Pipeline;
using Rebus.Transport;

namespace Rebus.Redis.Outbox;

[StepDocumentation("Adds a transaction-less Redis connection to the current transaction context if not present.")]
internal class OutboxOutgoingStep : IOutgoingStep
{
    private readonly RedisProvider _redis;

    public OutboxOutgoingStep(RedisProvider redis)
    {
        _redis = redis;
    }

    public Task Process(OutgoingStepContext context, Func<Task> next)
    {
        var transactionContext = context.Load<ITransactionContext>();

        if (transactionContext.Items.ContainsKey(RedisProvider.CurrentOutboxConnectionKey))
        {
            return next();
        }

        var outboxConnection = _redis.GetWithoutTransaction();
        transactionContext.Items[RedisProvider.CurrentOutboxConnectionKey] = outboxConnection;

        return next();
    }
}