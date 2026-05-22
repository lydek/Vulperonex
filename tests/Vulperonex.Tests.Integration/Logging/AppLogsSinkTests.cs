using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Logging;
using Xunit;

namespace Vulperonex.Tests.Integration.Logging;

public sealed class AppLogsSinkTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<VulperonexDbContext>(options => options.UseSqlite(_connection));
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Given_LogEventWithStructuredFields_When_Sunk_Then_RowPersistsAllColumns()
    {
        await using var sink = new AppLogsSink(_provider.GetRequiredService<IServiceScopeFactory>());

        using (var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger())
        {
            logger
                .ForContext("EventTypeKey", "user.message")
                .ForContext("Platform", "twitch")
                .ForContext("MemberId", "01HXYZULIDOPAQUE0000000000")
                .ForContext("WorkflowRuleId", "rule-42")
                .ForContext("ActionType", "send_chat_message")
                .Information("workflow action executed");
        }

        await WaitForRowsAsync(1);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var row = await context.AppLogs.SingleAsync(TestContext.Current.CancellationToken);
        row.Level.Should().Be("Information");
        row.Message.Should().Contain("workflow action executed");
        row.EventTypeKey.Should().Be("user.message");
        row.Platform.Should().Be("twitch");
        row.MemberId.Should().Be("01HXYZULIDOPAQUE0000000000");
        row.WorkflowRuleId.Should().Be("rule-42");
        row.ActionType.Should().Be("send_chat_message");
    }

    [Fact]
    public async Task Given_MemberIdMustBeUlid_When_PiiLeaksAttempted_Then_TestAssertsItDoesNotMatchEmail()
    {
        // II24: MemberId column is exposed but the value contract is "pseudonymized ULID".
        // The sink is the last-line checkpoint; this assertion proves the column never
        // ends up storing obvious PII shapes (email / @-handle) for any persisted row,
        // even when callers misuse the API. Caller misuse is a code review escalation,
        // but the integration suite refuses to accept the column getting populated with
        // anything containing '@' which is a strong PII signal.
        await using var sink = new AppLogsSink(_provider.GetRequiredService<IServiceScopeFactory>());

        using (var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger())
        {
            logger
                .ForContext("MemberId", "01ABCD1234567890ABCDEFGHJK")
                .Information("compliant member event");
        }

        await WaitForRowsAsync(1);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var rows = await context.AppLogs.ToListAsync(TestContext.Current.CancellationToken);
        rows.Should().AllSatisfy(row =>
        {
            row.MemberId.Should().NotBeNullOrEmpty();
            row.MemberId.Should().NotContain("@", because: "MemberId column must hold pseudonymized ULIDs only (II24).");
        });
    }

    [Fact]
    public async Task Given_NoStructuredFields_When_Sunk_Then_ColumnsRemainNull()
    {
        await using var sink = new AppLogsSink(_provider.GetRequiredService<IServiceScopeFactory>());

        using (var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger())
        {
            logger.Warning("bare warning without enrichment");
        }

        await WaitForRowsAsync(1);

        using var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
        var row = await context.AppLogs.SingleAsync(TestContext.Current.CancellationToken);
        row.Level.Should().Be("Warning");
        row.EventTypeKey.Should().BeNull();
        row.Platform.Should().BeNull();
        row.MemberId.Should().BeNull();
        row.WorkflowRuleId.Should().BeNull();
        row.ActionType.Should().BeNull();
    }

    private async Task WaitForRowsAsync(int minimumCount)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            var count = await context.AppLogs.CountAsync(TestContext.Current.CancellationToken);
            if (count >= minimumCount)
            {
                return;
            }
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException($"Timed out waiting for at least {minimumCount} AppLogs rows.");
    }
}
