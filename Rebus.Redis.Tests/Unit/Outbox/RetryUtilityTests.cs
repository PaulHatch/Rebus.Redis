using FluentAssertions;
using Rebus.Redis.Outbox;
using Xunit;

namespace Rebus.Redis.Tests.Unit.Outbox;

public class RetryUtilityTests
{
    [Fact]
    public async Task ExecuteAsync_SuccessOnFirstTry_ShouldNotRetry()
    {
        var attemptCount = 0;
        var delays = new[] { TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20) };
        var retryUtility = new RetryUtility(delays);
        
        await retryUtility.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.Delay(1);
        });

        attemptCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FailuresThenSuccess_ShouldRetryUntilSuccess()
    {
        var attemptCount = 0;
        const int successOnAttempt = 3;
        var delays = new[] { 
            TimeSpan.FromMilliseconds(10), 
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(30) 
        };
        var retryUtility = new RetryUtility(delays);
        
        await retryUtility.ExecuteAsync(async () =>
        {
            attemptCount++;
            if (attemptCount < successOnAttempt)
            {
                throw new InvalidOperationException($"Attempt {attemptCount} failed");
            }
            await Task.Delay(1);
        });

        attemptCount.Should().Be(successOnAttempt);
    }

    [Fact]
    public async Task ExecuteAsync_AllFailures_ShouldThrowLastException()
    {
        var attemptCount = 0;
        var delays = new[] { 
            TimeSpan.FromMilliseconds(10), 
            TimeSpan.FromMilliseconds(20) 
        };
        var retryUtility = new RetryUtility(delays);
        
        var action = async () => await retryUtility.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.Delay(1);
            throw new InvalidOperationException($"Attempt {attemptCount} failed");
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Attempt 3 failed"); // Initial attempt + 2 retries
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectDelays()
    {
        var attemptTimes = new List<DateTime>();
        var delays = new[] { 
            TimeSpan.FromMilliseconds(50), 
            TimeSpan.FromMilliseconds(100) 
        };
        var retryUtility = new RetryUtility(delays);
        
        try
        {
            await retryUtility.ExecuteAsync(async () =>
            {
                attemptTimes.Add(DateTime.UtcNow);
                await Task.Delay(1);
                throw new InvalidOperationException("Always fails");
            });
        }
        catch
        {
            // Expected to fail
        }

        attemptTimes.Should().HaveCount(3); // Initial + 2 retries
        
        // Verify delays (with some tolerance for timing)
        var firstDelay = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
        firstDelay.Should().BeGreaterThan(40); // 50ms with tolerance
        
        var secondDelay = (attemptTimes[2] - attemptTimes[1]).TotalMilliseconds;
        secondDelay.Should().BeGreaterThan(90); // 100ms with tolerance
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ShouldRespectCancellation()
    {
        using var cts = new CancellationTokenSource();
        var attemptCount = 0;
        var delays = new[] { 
            TimeSpan.FromMilliseconds(100), 
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100) 
        };
        var retryUtility = new RetryUtility(delays);
        
        var task = retryUtility.ExecuteAsync(async () =>
        {
            attemptCount++;
            if (attemptCount == 2)
            {
                cts.Cancel();
            }
            await Task.Delay(1);
            throw new InvalidOperationException("Failed");
        }, cts.Token);

        await task; // Should return without throwing due to cancellation
        
        attemptCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoDelays_ShouldOnlyTryOnce()
    {
        var attemptCount = 0;
        var retryUtility = new RetryUtility([]);
        
        var action = async () => await retryUtility.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.Delay(1);
            throw new InvalidOperationException("Failed");
        });

        await action.Should().ThrowAsync<InvalidOperationException>();
        attemptCount.Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNullDelays_ShouldThrowArgumentNullException()
    {
        var action = () => new RetryUtility(null!);
        
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("delays");
    }

    [Fact]
    public async Task ExecuteAsync_DifferentExceptionTypes_ShouldRetryForAll()
    {
        var attemptCount = 0;
        var exceptions = new Exception[]
        {
            new InvalidOperationException("Error 1"),
            new ArgumentException("Error 2"),
            new TimeoutException("Error 3")
        };
        var delays = new[] { 
            TimeSpan.FromMilliseconds(10), 
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(10) 
        };
        var retryUtility = new RetryUtility(delays);
        
        await retryUtility.ExecuteAsync(async () =>
        {
            if (attemptCount < exceptions.Length)
            {
                var ex = exceptions[attemptCount];
                attemptCount++;
                throw ex;
            }
            attemptCount++;
            await Task.Delay(1);
        });

        attemptCount.Should().Be(exceptions.Length + 1);
    }
}