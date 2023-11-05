using Rebus.Pipeline;
using Rebus.Transport;

namespace Rebus.Redis.Outbox;

/// <summary>
/// Step to provide a Redis transaction to the current pipeline context
/// </summary>
internal class OutboxIncomingStep : IIncomingStep
{
    private readonly RedisProvider _provider;

    public OutboxIncomingStep(RedisProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var outboxConnection = _provider.GetWithTransaction();
        var transactionContext = context.Load<ITransactionContext>();

        transactionContext.Items[RedisProvider.CurrentOutboxConnectionKey] = outboxConnection;

        transactionContext.OnCommit(async _ => await outboxConnection.Commit());

        await next();
    }
}