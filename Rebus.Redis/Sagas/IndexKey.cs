using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace Rebus.Redis.Sagas;

/// <summary>
/// Represents the key and hash property of an index.
/// </summary>
/// <param name="Key">The redis lookup hash.</param>
/// <param name="HashField">A field name representing the hash property of the index.</param>
[JsonConverter(typeof(IndexKeyConverter))]
internal record IndexKey(RedisKey Key, RedisValue HashField);