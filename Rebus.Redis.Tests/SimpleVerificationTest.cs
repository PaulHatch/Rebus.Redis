using Xunit;
using FluentAssertions;
using NSubstitute;
using StackExchange.Redis;

namespace Rebus.Redis.Tests;

public class SimpleVerificationTest
{
    [Fact]
    public void InternalsVisibleTo_ShouldAllowAccessToInternalClasses()
    {
        // Arrange
        var connection = Substitute.For<IConnectionMultiplexer>();
        var database = Substitute.For<IDatabase>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        
        // Act - This will only compile if InternalsVisibleTo is working
        var redisProvider = new RedisProvider(connection);
        
        // Assert
        redisProvider.Should().NotBeNull();
        redisProvider.Database.Should().NotBeNull();
    }
    
    [Fact] 
    public void TestSetup_IsWorking()
    {
        // Simple test to verify xUnit and FluentAssertions are working
        var result = 2 + 2;
        result.Should().Be(4);
    }
}