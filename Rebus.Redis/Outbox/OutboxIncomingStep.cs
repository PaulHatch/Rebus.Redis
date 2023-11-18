using System;
using System.Threading.Tasks;
using Rebus.Pipeline;
using Rebus.Transport;

namespace Rebus.Redis.Outbox;

/// <summary>
/// Step to provide a Redis transaction to the current pipeline context
/// </summary>
[StepDocumentation(
    "Adds a new Redis connection with transaction and registers committing it with the current Rebus transaction context."
)]
internal class OutboxIncomingStep : IIncomingStep
{
    private readonly RedisProvider _provider;

    public OutboxIncomingStep(RedisProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public Task Process(IncomingStepContext context, Func<Task> next)
    {
        var outboxConnection = _provider.GetWithTransaction();
        var transactionContext = context.Load<ITransactionContext>();

        transactionContext.Items[RedisProvider.CurrentOutboxConnectionKey] = outboxConnection;

        transactionContext.OnCommit(async _ => await outboxConnection.Commit());

        return next();
    }
}