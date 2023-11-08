using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Transport;
using StackExchange.Redis;

namespace Rebus.Redis.Outbox;

internal class RedisOutboxStorage : IOutboxStorage, IInitializable
{
    private const string _bodyKey = "body";
    private const string _addressKey = "address";

    private readonly RedisKey _outboxName;
    private readonly IDatabaseAsync _database;
    private readonly RedisValue _groupName;
    private readonly RedisValue _consumerName;
    private readonly ILog _log;
    private readonly RedisOutboxConfiguration _config;
    private readonly IConnectionMultiplexer? _dedicatedReaderConnection;

    public RedisOutboxStorage(
        string outboxName,
        RedisProvider redisProvider,
        RedisOutboxConfiguration config,
        IRebusLoggerFactory loggerFactory)
    {
        if (config.UseBlockingRead)
        {
            var opts = ConfigurationOptions.Parse(redisProvider.Database.Multiplexer.Configuration);
            opts.ClientName = "rebus-redis-outbox";
            opts.AsyncTimeout = Math.Max((int)(config.ForwardingInterval.TotalMilliseconds * 2), opts.AsyncTimeout);
            _dedicatedReaderConnection = ConnectionMultiplexer.Connect(opts.ToString());
        }

        _outboxName = outboxName;
        _database = redisProvider.Database;
        _groupName = config.ConsumerGroupName;
        _consumerName = config.ConsumerName;
        _config = config;
        _log = loggerFactory.GetLogger<RedisOutboxStorage>();
    }

    public Task Save(OutgoingTransportMessage message, RedisTransaction transaction)
    {
        var streamPairs = new NameValueEntry[message.TransportMessage.Headers.Count + 2];
        streamPairs[0] = new NameValueEntry(_addressKey, message.DestinationAddress);
        streamPairs[1] = new NameValueEntry(_bodyKey, message.TransportMessage.Body);
        var i = 2;
        foreach (var header in message.TransportMessage.Headers)
        {
            streamPairs[i] = new NameValueEntry("h-" + header.Key, header.Value);
            i++;
        }

        transaction.InTransaction(t => t.StreamAddAsync(_outboxName, streamPairs));

        return transaction.Task;
    }

    public async Task TrimQueue()
    {
        await _database.StreamTrimAsync(_outboxName, _config.TrimSize, true);
    }

    public async Task CleanupIdleConsumers()
    {
        var consumerDetails = await _database.StreamConsumerInfoAsync(_outboxName, _groupName);

        foreach (var detail in consumerDetails)
        {
            if (TimeSpan.FromMilliseconds(detail.IdleTimeInMilliseconds) > _config.IdleConsumerTimeout)
            {
                await _database.StreamDeleteConsumerAsync(_outboxName, _groupName, detail.Name);
                _log.Info("Removed inactive consumer: {ConsumerName}", detail.Name);
            }
        }
    }

    public async Task<IEnumerable<OutboxMessage>?> GetOrphanedMessageBatch()
    {
        var claims = await _database.StreamAutoClaimAsync(
            _outboxName,
            _groupName,
            _consumerName,
            _config.OrphanedMessageTimeout.Milliseconds,
            ">",
            _config.MessageBatchSize);

        return MapBatch(claims.ClaimedEntries);
    }


    public async Task<IEnumerable<OutboxMessage>?> GetNextMessageBatch()
    {
        if (_dedicatedReaderConnection is not null)
        {
            // TODO: If StackExchange Redis adds support for blocking operations, remove the explicit blocking reader and
            // switch to using the built-in blocking reader.
            // See:
            // - https://github.com/StackExchange/StackExchange.Redis/issues/1961
            // - https://github.com/StackExchange/StackExchange.Redis/issues/2147

            var db = _dedicatedReaderConnection.GetDatabase();

            return await db.BlockingGetMessages(
                _outboxName,
                _groupName,
                _consumerName,
                _config.ForwardingInterval,
                _config.MessageBatchSize);
        }

        var batch = await _database.StreamReadGroupAsync(
            _outboxName,
            _groupName,
            _consumerName,
            ">",
            _config.MessageBatchSize,
            true);

        return batch.Length == 0 ? null : MapBatch(batch);
    }

    private IEnumerable<OutboxMessage> MapBatch(StreamEntry[] batch)
    {
        foreach (var message in batch)
        {
            var outboxMessage = new OutboxMessage(message.Id);
            foreach (var value in message.Values)
            {
                switch (value.Name)
                {
                    case _bodyKey:
                        outboxMessage.Body = value.Value;
                        break;
                    case _addressKey:
                        outboxMessage.DestinationAddress = value.Value;
                        break;
                    default:
                        if (value.Name.StartsWith("h-"))
                        {
                            outboxMessage.Headers.Add(
                                value.Name.ToString().Substring(2),
                                value.Value.ToString());
                        }
                        else
                        {
                            throw new InvalidDataException($"Unknown key {value.Name}");
                        }

                        break;
                }
            }

            yield return outboxMessage;
        }
    }

    public Task MarkAsDispatched(OutboxMessage message)
    {
        return _database.StreamAcknowledgeAsync(_outboxName, _groupName, message.Id);
    }

    public void Initialize()
    {
        try
        {
            _database.StreamCreateConsumerGroupAsync(_outboxName, _groupName, "0-0");
            _log.Info("Created consumer group {GroupName} for {}", _groupName, _outboxName);
        }
        catch (RedisServerException)
        {
        }
    }
}