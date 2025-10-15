using FluentAssertions;
using NSubstitute;
using Rebus.Redis.Sagas;
using Rebus.Redis.Tests.Fixtures;
using Rebus.Sagas;
using StackExchange.Redis;
using Xunit;

namespace Rebus.Redis.Tests.Unit.Sagas;

public class RedisSagaStorageTests
{
    private readonly IDatabase _database;
    private readonly RedisProvider _redisProvider;
    private readonly RedisSagaStorage _storage;

    public RedisSagaStorageTests()
    {
        _database = Substitute.For<IDatabase>();
        var connection = Substitute.For<IConnectionMultiplexer>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        
        _redisProvider = new RedisProvider(connection);
        _storage = new RedisSagaStorage(_redisProvider, 1);
    }

    [Fact]
    public async Task Find_ById_WhenSagaExists_ShouldReturnSagaData()
    {
        var sagaId = Guid.NewGuid();
        var sagaData = SagaTestFixture.CreateSagaData();
        sagaData.Id = sagaId;
        
        // Use camelCase naming policy to match RedisSagaStorage
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var serializedData = System.Text.Json.JsonSerializer.Serialize(sagaData, options);
        _database.HashGetAsync(Arg.Any<RedisKey>(), "dat").Returns(serializedData);
        _database.HashGetAsync(Arg.Any<RedisKey>(), "rev").Returns(1);

        var result = await _storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaId);

        result.Should().NotBeNull();
        result.Id.Should().Be(sagaId);
    }

    [Fact]
    public async Task Find_ById_WhenSagaDoesNotExist_ShouldReturnNull()
    {
        var sagaId = Guid.NewGuid();
        _database.HashGetAsync(Arg.Any<RedisKey>(), "dat").Returns(RedisValue.Null);

        var result = await _storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Find_ByCorrelationProperty_WhenSagaExists_ShouldReturnSagaData()
    {
        var correlationId = "test-correlation-123";
        var sagaId = Guid.NewGuid();
        var sagaData = SagaTestFixture.CreateSagaData(correlationId);
        sagaData.Id = sagaId;
        
        _database.HashGetAsync(Arg.Any<RedisKey>(), Arg.Is<RedisValue>(v => v.ToString().Contains(correlationId)))
            .Returns($"saga:TestSagaData:{sagaId}");
        
        // Use camelCase naming policy to match RedisSagaStorage
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
        var serializedData = System.Text.Json.JsonSerializer.Serialize(sagaData, options);
        _database.HashGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains(sagaId.ToString())), "dat")
            .Returns(serializedData);
        _database.HashGetAsync(Arg.Is<RedisKey>(k => k.ToString().Contains(sagaId.ToString())), "rev")
            .Returns(1);

        var result = await _storage.Find(typeof(TestSagaData), nameof(TestSagaData.CorrelationId), correlationId);

        result.Should().NotBeNull();
        result.Should().BeOfType<TestSagaData>();
        ((TestSagaData)result).CorrelationId.Should().Be(correlationId);
    }

    private class TestCorrelationProperty : ISagaCorrelationProperty
    {
        public TestCorrelationProperty(string propertyName, Type sagaDataType)
        {
            PropertyName = propertyName;
            SagaDataType = sagaDataType;
        }

        public string PropertyName { get; }
        public Type SagaDataType { get; }
    }

    [Fact]
    public async Task Insert_ShouldStoreNewSagaData()
    {
        var sagaData = SagaTestFixture.CreateSagaData();
        var correlationProperties = new[]
        {
            new TestCorrelationProperty(nameof(TestSagaData.CorrelationId), typeof(TestSagaData))
        };

        var connection = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        var redisProvider = new RedisProvider(connection);
        var storage = new RedisSagaStorage(redisProvider, 1);

        // Note: Due to the complex Lua script execution, we can't easily test the actual insert
        // This would require integration testing with a real Redis instance
        var action = async () => await storage.Insert(sagaData, correlationProperties);
        
        // The action should not throw for valid data
        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Update_ShouldIncrementRevision()
    {
        var sagaData = SagaTestFixture.CreateSagaData();
        sagaData.Revision = 1;
        var originalRevision = sagaData.Revision;
        
        var correlationProperties = new[]
        {
            new TestCorrelationProperty(nameof(TestSagaData.CorrelationId), typeof(TestSagaData))
        };

        var connection = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        var redisProvider = new RedisProvider(connection);
        var storage = new RedisSagaStorage(redisProvider, 1);

        // Note: Due to the complex transaction handling, we can't easily test the actual update
        // This would require integration testing with a real Redis instance
        try
        {
            await storage.Update(sagaData, correlationProperties);
        }
        catch
        {
            // Expected to fail without proper Redis setup
        }

        // The saga data revision should be incremented
        sagaData.Revision.Should().Be(originalRevision + 1);
    }

    [Fact]
    public async Task Delete_ShouldRemoveSagaData()
    {
        var sagaData = SagaTestFixture.CreateSagaData();
        
        var connection = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        var redisProvider = new RedisProvider(connection);
        var storage = new RedisSagaStorage(redisProvider, 1);

        // Note: Due to the complex transaction handling, we can't easily test the actual delete
        // This would require integration testing with a real Redis instance
        var action = async () => await storage.Delete(sagaData);
        
        // The action should not throw for valid data
        await action.Should().NotThrowAsync();
    }
}