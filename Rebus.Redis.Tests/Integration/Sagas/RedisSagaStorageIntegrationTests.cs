using FluentAssertions;
using Rebus.Exceptions;
using Rebus.Redis.Sagas;
using Rebus.Redis.Tests.Fixtures;
using Rebus.Sagas;
using Xunit;

namespace Rebus.Redis.Tests.Integration.Sagas;

public class RedisSagaStorageIntegrationTests : IClassFixture<RedisTestFixture>
{
    private readonly RedisTestFixture _redisFixture;

    public RedisSagaStorageIntegrationTests(RedisTestFixture redisFixture)
    {
        _redisFixture = redisFixture;
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
    public async Task SagaStorage_ShouldPersistAndRetrieveSagaData()
    {
        await _redisFixture.FlushDatabaseAsync();

        var redisProvider = new RedisProvider(_redisFixture.Connection);
        var storage = new RedisSagaStorage(redisProvider, 1);

        var sagaData = SagaTestFixture.CreateSagaData("test-correlation-123");
        var correlationProperties = new[]
        {
            new TestCorrelationProperty(nameof(TestSagaData.CorrelationId), typeof(TestSagaData))
        };

        // Insert saga
        await storage.Insert(sagaData, correlationProperties);

        // Retrieve by ID
        var retrievedById = await storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaData.Id);

        retrievedById.Should().NotBeNull();
        retrievedById.Id.Should().Be(sagaData.Id);
        ((TestSagaData) retrievedById).CorrelationId.Should().Be("test-correlation-123");

        // Retrieve by correlation property
        var retrievedByCorrelation = await storage.Find(
            typeof(TestSagaData),
            nameof(TestSagaData.CorrelationId),
            "test-correlation-123");

        retrievedByCorrelation.Should().NotBeNull();
        retrievedByCorrelation.Id.Should().Be(sagaData.Id);
    }

    [Fact]
    public async Task SagaStorage_ShouldHandleConcurrentUpdates()
    {
        await _redisFixture.FlushDatabaseAsync();

        var redisProvider = new RedisProvider(_redisFixture.Connection);
        var storage = new RedisSagaStorage(redisProvider, 1);

        var sagaData = SagaTestFixture.CreateSagaData("concurrent-test");
        var correlationProperties = new[]
        {
            new TestCorrelationProperty(nameof(TestSagaData.CorrelationId), typeof(TestSagaData))
        };

        await storage.Insert(sagaData, correlationProperties);

        // Load saga twice to simulate concurrent access
        var saga1 = await storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaData.Id);
        var saga2 = await storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaData.Id);

        saga1.Should().NotBeNull();
        saga2.Should().NotBeNull();

        // Update first instance
        ((TestSagaData) saga1).ProcessingCount = 10;
        await storage.Update(saga1, correlationProperties);

        // Try to update second instance - should fail with optimistic concurrency
        ((TestSagaData) saga2).ProcessingCount = 20;
        var updateAction = async () => await storage.Update(saga2, correlationProperties);

        // The actual exception might be RedisServerException with revision-mismatch message
        // or ConcurrencyException depending on how the error is handled
        await updateAction.Should().ThrowAsync<Exception>()
            .Where(e =>
                e is ConcurrencyException ||
                (e is StackExchange.Redis.RedisServerException &&
                 ((StackExchange.Redis.RedisServerException) e).Message.Contains("revision-mismatch")));
    }

    [Fact]
    public async Task SagaStorage_ShouldDeleteSagaAndIndexes()
    {
        await _redisFixture.FlushDatabaseAsync();

        var redisProvider = new RedisProvider(_redisFixture.Connection);
        var storage = new RedisSagaStorage(redisProvider, 1);

        var sagaData = SagaTestFixture.CreateSagaData("delete-test");
        var correlationProperties = new[]
        {
            new TestCorrelationProperty(nameof(TestSagaData.CorrelationId), typeof(TestSagaData))
        };

        await storage.Insert(sagaData, correlationProperties);

        // Verify saga exists
        var retrieved = await storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaData.Id);
        retrieved.Should().NotBeNull();

        // Delete saga
        await storage.Delete(retrieved);

        // Verify saga is deleted
        var afterDelete = await storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaData.Id);
        afterDelete.Should().BeNull();

        // Verify index is also deleted
        var byCorrelation = await storage.Find(
            typeof(TestSagaData),
            nameof(TestSagaData.CorrelationId),
            "delete-test");
        byCorrelation.Should().BeNull();
    }

    [Fact]
    public async Task SagaStorage_WithSharding_ShouldDistributeIndexes()
    {
        await _redisFixture.FlushDatabaseAsync();

        var redisProvider = new RedisProvider(_redisFixture.Connection);
        var storage = new RedisSagaStorage(redisProvider, 10); // Use 10 shards

        var correlationProperties = new[]
        {
            new TestCorrelationProperty(nameof(TestSagaData.CorrelationId), typeof(TestSagaData))
        };

        // Insert multiple sagas
        var sagas = Enumerable.Range(0, 20)
            .Select(i => SagaTestFixture.CreateSagaData($"correlation-{i}"))
            .ToList();

        foreach (var saga in sagas)
        {
            await storage.Insert(saga, correlationProperties);
        }

        // Verify all sagas can be retrieved
        foreach (var saga in sagas)
        {
            var retrieved = await storage.Find(
                typeof(TestSagaData),
                nameof(TestSagaData.CorrelationId),
                saga.CorrelationId);

            retrieved.Should().NotBeNull();
            retrieved.Id.Should().Be(saga.Id);
        }
    }

    [Fact]
    public async Task SagaStorage_ShouldUpdateSagaData()
    {
        await _redisFixture.FlushDatabaseAsync();

        var redisProvider = new RedisProvider(_redisFixture.Connection);
        var storage = new RedisSagaStorage(redisProvider, 1);

        var sagaData = SagaTestFixture.CreateSagaData("update-test");
        var correlationProperties = new[]
        {
            new TestCorrelationProperty(nameof(TestSagaData.CorrelationId), typeof(TestSagaData))
        };

        // Insert saga
        await storage.Insert(sagaData, correlationProperties);
        sagaData.Revision.Should().Be(1);

        // Update saga
        sagaData.ProcessingCount = 42;
        ((TestSagaData) sagaData).Description = "Updated description";
        await storage.Update(sagaData, correlationProperties);
        sagaData.Revision.Should().Be(2);

        // Retrieve and verify updates
        var retrieved = await storage.Find(typeof(TestSagaData), nameof(ISagaData.Id), sagaData.Id);
        retrieved.Should().NotBeNull();
        ((TestSagaData) retrieved).ProcessingCount.Should().Be(42);
        ((TestSagaData) retrieved).Description.Should().Be("Updated description");
        retrieved.Revision.Should().Be(2);
    }
}