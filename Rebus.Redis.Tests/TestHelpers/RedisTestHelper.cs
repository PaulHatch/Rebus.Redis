using StackExchange.Redis;

namespace Rebus.Redis.Tests.TestHelpers;

public static class RedisTestHelper
{
    public static async Task<bool> IsRedisAvailable(string connectionString)
    {
        try
        {
            using var connection = await ConnectionMultiplexer.ConnectAsync(connectionString);
            return connection.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    public static async Task WaitForRedis(string connectionString, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (await IsRedisAvailable(connectionString))
            {
                return;
            }
            
            await Task.Delay(500);
        }
        
        throw new TimeoutException($"Redis did not become available within {timeout}");
    }

    public static async Task<string[]> GetAllKeys(IDatabase database, string pattern = "*")
    {
        var server = database.Multiplexer.GetServer(database.Multiplexer.GetEndPoints()[0]);
        var keys = new List<string>();
        
        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            keys.Add(key.ToString());
        }
        
        return keys.ToArray();
    }

    public static async Task CleanupTestData(IDatabase database, string prefix)
    {
        var server = database.Multiplexer.GetServer(database.Multiplexer.GetEndPoints()[0]);
        
        await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
        {
            await database.KeyDeleteAsync(key);
        }
    }
}