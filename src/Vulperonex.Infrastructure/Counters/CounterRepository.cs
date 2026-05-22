using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Counters;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;

namespace Vulperonex.Infrastructure.Counters;

public sealed class CounterRepository(VulperonexDbContext context, TimeProvider timeProvider) : ICounterRepository
{
    public async Task<long> IncrementAsync(string key, long delta, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Counter key is required.", nameof(key));
        }

        var counter = await context.Counters.FirstOrDefaultAsync(
            existing => existing.Key == key,
            cancellationToken);

        if (counter is null)
        {
            counter = new CounterEntity
            {
                Key = key,
            };
            context.Counters.Add(counter);
        }

        counter.Value += delta;
        counter.UpdatedAt = timeProvider.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        return counter.Value;
    }
}
