using Rebus.Handlers;
using Rebus.Sagas;

namespace Rebus.Redis.Tests.Fixtures;

public class SagaTestFixture
{
    public static TestSagaData CreateSagaData(string? correlationId = null)
    {
        return new TestSagaData
        {
            Id = Guid.NewGuid(),
            Revision = 0,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            ProcessingStarted = DateTime.UtcNow,
            Items = []
        };
    }

    public static TestSagaData CreateSagaDataWithItems(int itemCount = 5)
    {
        var sagaData = CreateSagaData();
        for (int i = 0; i < itemCount; i++)
        {
            sagaData.Items.Add($"Item_{i}");
        }
        return sagaData;
    }
}

public class TestSagaData : ISagaData
{
    public Guid Id { get; set; }
    public int Revision { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime ProcessingStarted { get; set; }
    public List<string> Items { get; set; } = [];
    public string? Description { get; set; }
    public int ProcessingCount { get; set; }
    public bool IsCompleted { get; set; }
}

public class TestSaga : Saga<TestSagaData>, IAmInitiatedBy<StartTestSaga>, IHandleMessages<UpdateTestSaga>
{
    protected override void CorrelateMessages(ICorrelationConfig<TestSagaData> config)
    {
        config.Correlate<StartTestSaga>(m => m.CorrelationId, d => d.CorrelationId);
        config.Correlate<UpdateTestSaga>(m => m.CorrelationId, d => d.CorrelationId);
    }

    public Task Handle(StartTestSaga message)
    {
        Data.CorrelationId = message.CorrelationId;
        Data.ProcessingStarted = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task Handle(UpdateTestSaga message)
    {
        Data.ProcessingCount++;
        Data.Items.Add(message.Item);
        return Task.CompletedTask;
    }
}

public class StartTestSaga
{
    public string CorrelationId { get; set; } = string.Empty;
}

public class UpdateTestSaga
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
}