using System;
using Rebus.Transport;
using StackExchange.Redis;

namespace Rebus.Redis.Outbox;

public static class OutboxExtensions
{
    /// <summary>
    /// Enables the use of outbox on the <see cref="RebusTransactionScope"/>. Will enlist all outgoing message operations in the
    /// /<paramref name="transaction"/> passed to the method.
    /// </summary>
    public static void UseOutbox(this RebusTransactionScope rebusTransactionScope, ITransaction transaction)
    {
        if (rebusTransactionScope == null)
        {
            throw new ArgumentNullException(nameof(rebusTransactionScope));
        }

        if (transaction == null)
        {
            throw new ArgumentNullException(nameof(transaction));
        }

        var context = rebusTransactionScope.TransactionContext;

        if (!context.Items.TryAdd(RedisProvider.CurrentOutboxConnectionKey, new RedisTransaction(transaction.Multiplexer, transaction)))
        {
            throw new InvalidOperationException("Cannot add the given connection/transaction to the current Rebus transaction, because a connection/transaction has already been added!");
        }
    }
}