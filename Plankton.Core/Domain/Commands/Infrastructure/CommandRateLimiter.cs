using Plankton.Core.Domain.ExceptionHandling;

namespace Plankton.Core.Domain.Commands.Infrastructure;

public sealed class CommandRateLimiter
{
    private readonly SemaphoreSlim _semaphore = new(5);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    public async Task ExecuteAsync(Func<Task> action)
    {
        if (!await _semaphore.WaitAsync(_timeout)) throw new RateLimitExceededException();

        try
        {
            await action();
        }
        finally
        {
            _semaphore.Release();
        }
    }
}