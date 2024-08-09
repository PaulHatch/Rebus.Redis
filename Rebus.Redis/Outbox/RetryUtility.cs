using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rebus.Redis.Outbox;

/// <summary>
/// Mini-Polly 🙂 - taken from Rebus.PostgreSql
/// </summary>
internal class RetryUtility
{
    private readonly List<TimeSpan> _delays;

    public RetryUtility(IEnumerable<TimeSpan> delays)
    {
        if (delays == null)
        {
            throw new ArgumentNullException(nameof(delays));
        }

        _delays = delays.ToList();
    }

    public async Task ExecuteAsync(Func<Task> execute, CancellationToken cancellationToken = default)
    {
        for (var index = 0; index <= _delays.Count; index++)
        {
            try
            {
                await execute();
                return;
            }
            catch
            {
                if (index == _delays.Count)
                {
                    throw;
                }

                var delay = _delays[index];

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}