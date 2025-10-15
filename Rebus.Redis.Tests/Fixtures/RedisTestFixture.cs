using DotNet.Testcontainers.Builders;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Rebus.Redis.Tests.Fixtures;

public class RedisTestFixture : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer;
    private IConnectionMultiplexer? _connection;

    public RedisTestFixture()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .Build();
    }

    public string ConnectionString => _redisContainer.GetConnectionString();
    
    public IConnectionMultiplexer Connection
    {
        get
        {
            if (_connection == null)
                throw new InvalidOperationException("Redis connection not initialized. Did you forget to call InitializeAsync?");
            return _connection;
        }
    }

    public IDatabase Database => Connection.GetDatabase();

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        var config = ConfigurationOptions.Parse(ConnectionString);
        config.AllowAdmin = true; // Enable admin mode for FLUSHDB
        _connection = await ConnectionMultiplexer.ConnectAsync(config);
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
        
        await _redisContainer.DisposeAsync();
    }

    public async Task FlushDatabaseAsync()
    {
        var server = Connection.GetServer(Connection.GetEndPoints()[0]);
        await server.FlushDatabaseAsync();
    }
}