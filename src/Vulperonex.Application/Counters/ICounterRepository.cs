namespace Vulperonex.Application.Counters;

public interface ICounterRepository
{
    Task<long> IncrementAsync(string key, long delta, CancellationToken cancellationToken = default);
}
