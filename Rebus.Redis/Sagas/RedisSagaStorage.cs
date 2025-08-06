using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Rebus.Exceptions;
using Rebus.Sagas;
using StackExchange.Redis;

namespace Rebus.Redis.Sagas;

internal class RedisSagaStorage : ISagaStorage
{
    private static readonly JsonSerializerOptions _serializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SagaPropertyAccessor _propertyAccessor = new();
    private readonly RedisProvider _redisProvider;
    private readonly int _shards;

    public RedisSagaStorage(RedisProvider redis, int shards)
    {
        _shards = shards;
        _redisProvider = redis;
    }

    /// <inheritdoc />
    public async Task<ISagaData> Find(Type sagaDataType, string propertyName, object propertyValue)
    {
        string key;
        if (propertyName.Equals(nameof(ISagaData.Id), StringComparison.OrdinalIgnoreCase))
        {
            key = GetKey(sagaDataType, propertyValue.ToString());
        }
        else
        {
            var indexKey = GetIndexKey(sagaDataType, propertyName, propertyValue.ToString());
            var lookup = await _redisProvider.Database.HashGetAsync(indexKey.Key, indexKey.HashField);
            if (lookup.IsNullOrEmpty)
            {
                return null!;
            }

            key = lookup.ToString();
        }

        var result = await _redisProvider.Database.HashGetAsync(key, "dat");
        if (result.IsNullOrEmpty)
        {
            return null!;
        }

        return (ISagaData) JsonSerializer.Deserialize(result.ToString(), sagaDataType, _serializeOptions)!;
    }

    /// <inheritdoc />
    public async Task Insert(ISagaData data, IEnumerable<ISagaCorrelationProperty> correlationProperties)
    {
        if (data.Id == Guid.Empty)
        {
            throw new InvalidOperationException($"Saga data {data.GetType()} has an uninitialized Id property!");
        }

        if (data.Revision != 0)
        {
            throw new InvalidOperationException(
                $"Attempted to insert saga data with ID {data.Id} and revision {data.Revision}, but revision must be 0 on first insert!");
        }

        data.Revision++;
        var indexKeys = GetIndexKeys(data, correlationProperties);
        var keys = new List<RedisKey> {GetKey(data)};
        var values = new List<RedisValue>
        {
            JsonSerializer.Serialize(data, data.GetType(), _serializeOptions),
            JsonSerializer.Serialize(indexKeys, _serializeOptions)
        };
        indexKeys.AddKeys(keys, values);

        var script = new StringBuilder( /*lang=lua*/
            """
            if redis.call('EXISTS', KEYS[1]) == 1 then
                return error("already-exists")
            end
            redis.call('HSET', KEYS[1], 'rev', 1, 'dat', ARGV[1], 'idx', ARGV[2])
            """);

        for (var i = 0; i < indexKeys.Count; i++)
        {
            // index keys start at KEYS[2] and the hash property values at ARGV[3]
            script.Append( /*lang=lua*/ $"\nredis.call('HSET', KEYS[{i + 2}], ARGV[{i + 3}], KEYS[1])");
        }

        var txn = _redisProvider.GetForScope();
        try
        {
            txn.InTransaction(t => t.ScriptEvaluateAsync(script.ToString(), keys.ToArray(), values.ToArray()));
        }
        catch (RedisServerException e) when (e.Message.Contains("already-exists"))
        {
            throw new ConcurrencyException($"Sage data for {data.GetType().Name} with ID {data.Id} already exists");
        }

        await txn.Task;
    }

    /// <inheritdoc />
    public async Task Update(ISagaData data, IEnumerable<ISagaCorrelationProperty> correlationProperties)
    {
        if (data.Id == Guid.Empty)
        {
            throw new InvalidOperationException($"Saga data {data.GetType()} has an uninitialized Id property!");
        }

        data.Revision++;

        var indexKeys = GetIndexKeys(data, correlationProperties);

        var keys = new List<RedisKey> {GetKey(data)};
        var values = new List<RedisValue>
        {
            data.Revision,
            JsonSerializer.Serialize(data, data.GetType(), _serializeOptions),
            JsonSerializer.Serialize(indexKeys, _serializeOptions)
        };
        indexKeys.AddKeys(keys, values);

        var script = new StringBuilder( /*lang=lua*/
            """
            local currentVersion = tonumber(redis.call('HGET', KEYS[1], 'rev'))
            if currentVersion + 1 ~= tonumber(ARGV[1]) then
                return error("revision-mismatch")
            end
            redis.call('HSET', KEYS[1], 'rev', ARGV[1], 'dat', ARGV[2], 'idx', ARGV[3])
            """);

        for (var i = 0; i < indexKeys.Count; i++)
        {
            // index keys start at KEYS[2] and the hash property values at ARGV[4]
            script.Append( /*lang=lua*/ $"\nredis.call('HSET', KEYS[{i + 2}], ARGV[{i + 4}], KEYS[1])");
        }

        var txn = _redisProvider.GetForScope();
        try
        {
            txn.InTransaction(t => t.ScriptEvaluateAsync(script.ToString(), keys.ToArray(), values.ToArray()));
        }
        catch (RedisServerException e) when (e.Message.Contains("revision-mismatch"))
        {
            throw new ConcurrencyException(
                $"Update failed for sage data for {data.GetType().Name} with ID {data.Id} due to revision mismatch, another update has been made to this data since it was loaded for the current operation.");
        }

        await txn.Task;
    }

    /// <inheritdoc />
    public async Task Delete(ISagaData sagaData)
    {
        var key = GetKey(sagaData);
        string? indexKeys = await _redisProvider.Database.HashGetAsync(key, "idx");
        var txn = _redisProvider.GetForScope();

        if (indexKeys is not null)
        {
            var metadataFields = JsonSerializer.Deserialize<IEnumerable<IndexKey>>(indexKeys, _serializeOptions);
            foreach (var metadataField in metadataFields ?? [])
            {
                txn.InTransaction(t => t.HashDeleteAsync(metadataField.Key, metadataField.HashField));
            }
        }

        txn.InTransaction(t => t.KeyDeleteAsync(key));
        await txn.Task;
    }

    private static string GetKey(ISagaData data)
    {
        return GetKey(data.GetType(), data.Id);
    }

    private static string GetKey(Type type, object? id)
    {
        return $"saga:{type.Name.ToKebabCase()}:{id ?? throw new ArgumentNullException(nameof(id))}";
    }

    private List<IndexKey> GetIndexKeys(ISagaData data,
        IEnumerable<ISagaCorrelationProperty> correlationProperties)
    {
        return correlationProperties
            .Where(property => !property.PropertyName.Equals(nameof(ISagaData.Id), StringComparison.OrdinalIgnoreCase))
            .Select(property => GetIndexKey(data, property))
            .OfType<IndexKey>()
            .ToList();
    }

    private IndexKey? GetIndexKey(ISagaData data, ISagaCorrelationProperty property)
    {
        var value = _propertyAccessor.GetValueFromSagaData(data, property)?.ToString();
        return value is null ? null : GetIndexKey(data.GetType(), property.PropertyName, value);
    }

    private IndexKey GetIndexKey(Type type, string key, object? value)
    {
        const uint seed = 42;
        var stringValue = value?.ToString() ?? throw new ArgumentNullException(nameof(value));
        ReadOnlySpan<byte> valueSpan = Encoding.UTF8.GetBytes(stringValue).AsSpan();
        var shard = MurmurHash3.Hash32(ref valueSpan, seed) % _shards;
        return new IndexKey(
            $"index:{type.Name.ToKebabCase()}:{key.ToLowerInvariant()}:{shard}",
            $"{type.Name.ToKebabCase()}:{key.ToLowerInvariant()}:{stringValue}");
    }
}

internal static class IndexKeyExtension
{
    /// <summary>
    /// Helper for adding index keys to key/value parameters lists.
    /// </summary>
    /// <param name="lookupKeys">Keys to add.</param>
    /// <param name="keys">Target keys parameter collection.</param>
    /// <param name="values">Target values parameter collection.</param>
    public static void AddKeys(this List<IndexKey> lookupKeys,
        ICollection<RedisKey> keys,
        ICollection<RedisValue> values)
    {
        foreach (var lookupKey in lookupKeys)
        {
            keys.Add(lookupKey.Key);
            values.Add(lookupKey.HashField);
        }
    }
}