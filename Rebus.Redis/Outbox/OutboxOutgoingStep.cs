using Rebus.Pipeline;
using Rebus.Transport;
using System.Transactions;

namespace Rebus.Redis.Outbox;

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

        // in Rebus handler context, outbox is initialized by incoming step
        // not in Rebus handler context and a rebus outbox transaction is explicitly (UseOutbox)
        // created and outbox is initialized
        if (transactionContext.Items.ContainsKey(RedisProvider.CurrentOutboxConnectionKey) ||
            Transaction.Current == null)
        {
            return next();
        }

        // if an ambient transaction exists, create an outbox connection without transaction to
        // connection enroll in current transaction
        var outboxConnection = _redis.GetWithoutTransaction();
        transactionContext.Items[RedisProvider.CurrentOutboxConnectionKey] = outboxConnection;

        return next();
    }
}