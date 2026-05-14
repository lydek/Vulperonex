using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Settings;
using Vulperonex.Infrastructure.Settings;
using Xunit;

namespace Vulperonex.Tests.Integration.Settings;

public sealed class SystemSettingsServiceTests
{
    [Fact]
    public async Task Given_MissingKey_When_GetAsync_Then_DefaultValueIsReturned()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var service = new SystemSettingsService(context);

        var value = await service.GetAsync(SystemSettingKey.BusChannelCapacity, 10_000, TestContext.Current.CancellationToken);

        value.Should().Be(10_000);
    }

    [Fact]
    public async Task Given_SetValue_When_GetAsync_Then_TypedValueIsReturnedAndKeyIsLowercase()
    {
        await using var fixture = new Infrastructure.SqliteFixture();
        await using var context = await fixture.CreateContextAsync();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var service = new SystemSettingsService(context);

        await service.SetAsync("BUS.CHANNEL_CAPACITY", 128, category: "runtime", TestContext.Current.CancellationToken);
        var value = await service.GetAsync("bus.channel_capacity", 0, TestContext.Current.CancellationToken);

        value.Should().Be(128);
        var row = await context.SystemSettings.SingleAsync(TestContext.Current.CancellationToken);
        row.Key.Should().Be("bus.channel_capacity");
        row.UpdatedAt.Should().BeAfter(DateTimeOffset.MinValue);
    }
}
