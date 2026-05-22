using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Infrastructure.Counters;
using Xunit;

namespace Vulperonex.Tests.Integration.Infrastructure;

public sealed class CounterRepositoryTests
{
    [Fact]
    public async Task Given_CounterKey_When_IncrementedTwice_Then_ValueIsAccumulated()
    {
        await using var fixture = new SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var repository = new CounterRepository(context, TimeProvider.System);

        var first = await repository.IncrementAsync("lottery.tickets.alice", 3, TestContext.Current.CancellationToken);
        var second = await repository.IncrementAsync("lottery.tickets.alice", 2, TestContext.Current.CancellationToken);

        first.Should().Be(3);
        second.Should().Be(5);
    }
}
