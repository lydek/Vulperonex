using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Web;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class Phase5EndpointTests
{
    [Fact]
    public async Task Given_WorkflowRuleEndpoints_When_RuleIsCreatedListedAndDeleted_Then_CrudUsesExpectedHttpSemantics()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var createResponse = await client.PostAsJsonAsync(
            "/api/rules",
            new
            {
                name = "Chat reply",
                eventTypeKey = "user.message",
                isEnabled = true,
                priority = 10,
                conditions = Array.Empty<object>(),
                actions = new object[] { new { type = "sendChatMessage", template = "hi" } },
                executionMode = "Serial",
                maxParallelism = 1,
            },
            TestContext.Current.CancellationToken);

        var createBody = await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created, "response body was {0}", createBody);
        createResponse.Headers.Location?.ToString().Should().StartWith("/api/rules/");

        var listResponse = await client.GetAsync("/api/rules", TestContext.Current.CancellationToken);
        var listJson = await listResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", listJson);
        listJson.Should().Contain("Chat reply");

        var location = createResponse.Headers.Location!.ToString();
        var deleteResponse = await client.DeleteAsync(location, TestContext.Current.CancellationToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Given_WorkflowRuleValidation_When_SystemEventIsSubmitted_Then_UnknownEventTypeKeyIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/rules",
            new
            {
                name = "Bad rule",
                eventTypeKey = "platform.connection_changed",
                isEnabled = true,
                priority = 0,
                conditions = Array.Empty<object>(),
                actions = Array.Empty<object>(),
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("UNKNOWN_EVENT_TYPE_KEY");
    }

    [Fact]
    public async Task Given_WorkflowRuleValidation_When_RuleInvokesItself_Then_CircularReferenceIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/rules",
            new
            {
                id = "self",
                name = "Bad rule",
                eventTypeKey = "user.message",
                isEnabled = true,
                priority = 0,
                conditions = Array.Empty<object>(),
                actions = new object[] { new { type = "invokeSubWorkflow", workflowId = "self" } },
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("CIRCULAR_WORKFLOW_REFERENCE");
    }

    [Fact]
    public async Task Given_EventTypesEndpoint_When_SimulationAdapterStarted_Then_SimulatableAliasesAreProjected()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/event-types", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("\"key\":\"user.message\"");
        json.Should().Contain("\"isSimulatable\":true");
        json.Should().NotContain("platform.connection_changed");
    }

    [Fact]
    public async Task Given_ConfigEndpoint_When_ProtectedOAuthKeyIsRequested_Then_DenylistRunsBeforeRegistry()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/config/oauth.unknown.refresh_token", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("OAUTH_CREDENTIAL_NAMESPACE");
    }

    [Fact]
    public async Task Given_MemberEndpoint_When_MemberExists_Then_ShowReturnsReadModel()
    {
        await using var app = await StartAppAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            context.Members.Add(new MemberEntity
            {
                MemberId = "member-1",
                CheckInCount = 2,
                TotalLoyalty = 10,
            });
            context.PlatformIdentities.Add(new PlatformIdentityEntity
            {
                MemberId = "member-1",
                Platform = "twitch",
                PlatformUserId = "u1",
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = CreateClient(app);
        var response = await client.GetAsync("/api/members/member-1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("member-1");
        json.Should().Contain("twitch");
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development",
            },
            configureDefaultLoopbackPorts: false);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Path"] = databasePath,
        });
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");

        var app = VulperonexWebApplication.Build(builder);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            .Single();

        return new HttpClient { BaseAddress = new Uri(address!) };
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
