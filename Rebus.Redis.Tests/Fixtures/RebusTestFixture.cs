using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Transport.InMem;
using Xunit;

namespace Rebus.Redis.Tests.Fixtures;

public class RebusTestFixture : IAsyncLifetime
{
    private readonly List<IDisposable> _disposables = [];
    private readonly InMemNetwork _network = new();
    
    public InMemNetwork Network => _network;

    public BuiltinHandlerActivator CreateActivator()
    {
        var activator = new BuiltinHandlerActivator();
        _disposables.Add(activator);
        return activator;
    }

    public IBus CreateBus(string queueName, Action<RebusConfigurer>? additionalConfig = null)
    {
        var activator = CreateActivator();
        
        var configurer = Configure.With(activator)
            .Transport(t => t.UseInMemoryTransport(_network, queueName))
            .Subscriptions(s => s.StoreInMemory())
            .Sagas(s => s.StoreInMemory());

        additionalConfig?.Invoke(configurer);
        
        var bus = configurer.Start();
        _disposables.Add(bus);
        
        return bus;
    }

    public IBus CreateBusWithRedis(string queueName, string redisConnectionString, Action<RebusRedisConfig>? redisConfig = null)
    {
        var activator = CreateActivator();
        
        var bus = Configure.With(activator)
            .Transport(t => t.UseInMemoryTransport(_network, queueName))
            .Options(o =>
            {
                o.SetBusName(queueName);
                o.EnableRedis(redisConnectionString, redisConfig ?? (_ => { }));
            })
            .Start();
            
        _disposables.Add(bus);
        return bus;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        
        _disposables.Clear();
        return Task.CompletedTask;
    }
}