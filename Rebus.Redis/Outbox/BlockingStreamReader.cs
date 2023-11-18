using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Rebus.Redis.Outbox;


internal static class BlockingStreamReader
{
    private const string _bodyKey = "body";
    private const string _addressKey = "address";

    public static async Task<List<OutboxMessage>?> BlockingGetMessages(
        this IDatabaseAsync db,
        RedisKey key, 
        RedisValue groupName, 
        RedisValue consumerName, 
        TimeSpan block,
        int count, 
        CommandFlags flags = CommandFlags.None)
    {
        // ReSharper disable StringLiteralTypo
        var response = await db.ExecuteAsync("XREADGROUP",
            "GROUP", groupName, consumerName,
            "COUNT", count,
            "BLOCK", block.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
            "NOACK",
            "STREAMS", key, ">");

        if (response.IsNull || response.Resp3Type != ResultType.Array)
            return null;
        
        var topic = (RedisResult[]) ((RedisResult[]) response)![0]!;
        var topicElements = (RedisResult[]) topic[1]!;
        
        var result = new List<OutboxMessage>();

        foreach (RedisResult[]? element in topicElements)
        {
            var id = element![0].ToString();
            var values = element[1].ToDictionary();
            
            var outboxMessage = new OutboxMessage(id);
            foreach (var value in values)
            {
                switch (value.Key)
                {
                    case _bodyKey:
                        outboxMessage.Body = (byte[])value.Value!;
                        break;
                    case _addressKey:
                        outboxMessage.DestinationAddress = value.Value.ToString();
                        break;
                    default:
                        if (value.Key.StartsWith("h-"))
                        {
                            outboxMessage.Headers.Add(
                                value.Key.Substring(2),
                                value.Value.ToString());
                        }
                        else
                        {
                            throw new InvalidDataException($"Unknown key {value.Key}");
                        }

                        break;
                }
            }
            result.Add(outboxMessage);
        }

        return result;
    }
}