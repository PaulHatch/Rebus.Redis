using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rebus.Redis;

/// <summary>
/// Manages Redis transactions and associated asynchronous operations.
/// </summary>
public class RedisTransaction
{
    private readonly IDatabaseAsync _database;
    private readonly List<Task> _tasks = new();
    private readonly ITransaction? _transaction;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisTransaction" /> class.
    /// </summary>
    /// <param name="multiplexer">The multiplexer for Redis.</param>
    public RedisTransaction(IConnectionMultiplexer multiplexer)
    {
        _database = multiplexer?.GetDatabase() ?? throw new ArgumentNullException(nameof(multiplexer));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisTransaction" /> class.
    /// </summary>
    /// <param name="multiplexer">The multiplexer for Redis.</param>
    /// <param name="transaction">The underlying Redis transaction.</param>
    public RedisTransaction(IConnectionMultiplexer multiplexer, ITransaction transaction)
    {
        _database = multiplexer?.GetDatabase() ?? throw new ArgumentNullException(nameof(multiplexer));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Await this task before exiting an operation if any transaction are executed to ensure all associated
    /// asynchronous operations are completed.
    /// </summary>
    public Task Task => _transaction is null ? Task.WhenAll(_tasks) : Task.CompletedTask;

    /// <summary>
    /// Runs an operation using the transaction, adding the resulting task to the transaction.
    /// </summary>
    /// <param name="operation">The operation to run.</param>
    public void InTransaction(Func<IDatabaseAsync, Task> operation)
    {
        _tasks.Add(operation(_transaction ?? _database));
    }

    /// <summary>
    /// Commits the Redis transaction and ensures all associated asynchronous operations are completed.
    /// </summary>
    public async Task Commit()
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.ExecuteAsync();
        await Task.WhenAll(_tasks);
    }
}