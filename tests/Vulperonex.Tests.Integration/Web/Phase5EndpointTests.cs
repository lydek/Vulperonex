using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Application.Auth;
using Vulperonex.Application.Workflows;
using Vulperonex.Infrastructure.Data;
using Vulperonex.Infrastructure.Data.Entities;
using Vulperonex.Infrastructure.Security;
using Vulperonex.Infrastructure.Workflows;
using Vulperonex.Web;
using Vulperonex.Web.Configuration;
using Vulperonex.Web.Ports;
using Vulperonex.Web.TwitchAuth;
using Xunit;

namespace Vulperonex.Tests.Integration.Web;

public sealed class Phase5EndpointTests
{
    [Fact]
    public async Task Given_WebHostStartsWithFreshDatabase_When_RulesAreListed_Then_DatabaseIsMigratedAutomatically()
    {
        await using var app = await StartAppAsync(manualMigration: false);
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/rules", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", body);
    }

    [Fact]
    public async Task Given_CliRunsAgainstLiveApi_When_SmokeCommandsExecute_Then_TheySucceed()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var ruleList = await VulperonexCli.RunAsync(["rule", "list"], client, output, error);
        var configGet = await VulperonexCli.RunAsync(["config", "get", "log.min_level"], client, output, error);
        var memberList = await VulperonexCli.RunAsync(["member", "list"], client, output, error);
        var simulateChat = await VulperonexCli.RunAsync(["simulate", "chat", "hello"], client, output, error);
        var simulateFollow = await VulperonexCli.RunAsync(["simulate", "follow"], client, output, error);
        var simulateSub = await VulperonexCli.RunAsync(["simulate", "sub"], client, output, error);

        ruleList.Should().Be(0);
        configGet.Should().Be(0);
        memberList.Should().Be(0);
        simulateChat.Should().Be(0);
        simulateFollow.Should().Be(0);
        simulateSub.Should().Be(0);
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwitchOAuthStart_When_ClientIdIsConfigured_Then_AuthorizeUrlUsesPkceAndLoopbackCallback()
    {
        await using var app = await StartAppAsync(["Twitch:ClientId", "client-1"]);
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/twitch/auth/start",
            new { callbackPort = 7979 },
            TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", body);
        body.Should().Contain("https://id.twitch.tv/oauth2/authorize");
        body.Should().Contain("client_id=client-1");
        body.Should().Contain("redirect_uri=http%3A%2F%2Flocalhost%3A7979%2Fauth%2Fcallback");
        body.Should().Contain("code_challenge=");
        body.Should().Contain("state");
    }

    [Theory]
    [InlineData(null, null, false, false, false)]
    [InlineData("client-1", null, false, false, false)]
    [InlineData("client-1", "secret-1", false, true, false)]
    [InlineData("client-1", "secret-1", true, true, true)]
    public async Task Given_TwitchAuthStatus_When_Called_Then_OnlyConfigurationBooleansAreReturned(
        string? clientId,
        string? clientSecret,
        bool storeRefreshToken,
        bool expectedClientSecretConfigured,
        bool expectedHasRefreshToken)
    {
        var configurationPairs = new[]
        {
            "Twitch:ClientId", clientId ?? string.Empty,
            "Twitch:ClientSecret", clientSecret ?? string.Empty,
        };
        await using var app = await StartAppAsync(configurationPairs);
        if (storeRefreshToken)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var tokenStore = scope.ServiceProvider.GetRequiredService<IOAuthTokenStore>();
            await tokenStore.StoreRefreshTokenAsync("twitch", "refresh-secret", TestContext.Current.CancellationToken);
        }

        using var client = CreateClient(app);
        var response = await client.GetAsync("/api/twitch/auth/status", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("clientIdConfigured").GetBoolean().Should().Be(clientId is not null);
        document.RootElement.GetProperty("clientSecretConfigured").GetBoolean().Should().Be(expectedClientSecretConfigured);
        document.RootElement.GetProperty("hasRefreshToken").GetBoolean().Should().Be(expectedHasRefreshToken);
        body.Should().NotContain("client-1");
        body.Should().NotContain("secret-1");
        body.Should().NotContain("refresh-secret");
    }

    [Fact]
    public async Task Given_TwitchOAuthCallback_When_StateMatches_Then_CodeIsExchangedAndRefreshTokenIsStored()
    {
        var tokenEndpoint = new RecordingTwitchTokenEndpoint();
        await using var app = await StartAppAsync(
            ["Twitch:ClientId", "client-1"],
            services => services.AddSingleton<ITwitchTokenEndpoint>(tokenEndpoint));
        using var client = CreateClient(app);
        var start = await client.PostAsJsonAsync(
            "/api/twitch/auth/start",
            new { callbackPort = 7979 },
            TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        using var startDocument = JsonDocument.Parse(await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var state = startDocument.RootElement.GetProperty("state").GetString();

        var complete = await client.PostAsJsonAsync(
            "/api/twitch/auth/complete",
            new { state, code = "code-1" },
            TestContext.Current.CancellationToken);

        var body = await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        complete.StatusCode.Should().Be(HttpStatusCode.NoContent, "response body was {0}", body);
        tokenEndpoint.ExchangeCode.Should().Be("code-1");
        tokenEndpoint.ExchangeVerifier.Should().NotBeNullOrWhiteSpace();
        await using var scope = app.Services.CreateAsyncScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<IOAuthTokenStore>();
        var refreshToken = await tokenStore.GetRefreshTokenAsync("twitch", TestContext.Current.CancellationToken);
        refreshToken.Should().Be("refresh-1");
    }

    [Fact]
    public async Task Given_TwitchOAuthCallback_When_TokenExchangeFails_Then_BadRequestErrorIsReturned()
    {
        await using var app = await StartAppAsync(
            ["Twitch:ClientId", "client-1"],
            services => services.AddSingleton<ITwitchTokenEndpoint>(new FailingTwitchTokenEndpoint()));
        using var client = CreateClient(app);
        var start = await client.PostAsJsonAsync(
            "/api/twitch/auth/start",
            new { callbackPort = 7979 },
            TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        using var startDocument = JsonDocument.Parse(await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var state = startDocument.RootElement.GetProperty("state").GetString();

        var complete = await client.PostAsJsonAsync(
            "/api/twitch/auth/complete",
            new { state, code = "bad-code" },
            TestContext.Current.CancellationToken);

        var body = await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        complete.StatusCode.Should().Be(HttpStatusCode.BadRequest, "response body was {0}", body);
        body.Should().Contain("TWITCH_OAUTH_EXCHANGE_FAILED");
    }

    [Fact]
    public async Task Given_TwitchOAuthCallback_When_ClientSecretIsMissing_Then_BadRequestErrorIsReturned()
    {
        await using var app = await StartAppAsync(
            ["Twitch:ClientId", "client-1"],
            services => services.AddSingleton<ITwitchTokenEndpoint>(new MissingSecretTwitchTokenEndpoint()));
        using var client = CreateClient(app);
        var start = await client.PostAsJsonAsync(
            "/api/twitch/auth/start",
            new { callbackPort = 7979 },
            TestContext.Current.CancellationToken);
        start.EnsureSuccessStatusCode();
        using var startDocument = JsonDocument.Parse(await start.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var state = startDocument.RootElement.GetProperty("state").GetString();

        var complete = await client.PostAsJsonAsync(
            "/api/twitch/auth/complete",
            new { state, code = "code-1" },
            TestContext.Current.CancellationToken);

        var body = await complete.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        complete.StatusCode.Should().Be(HttpStatusCode.BadRequest, "response body was {0}", body);
        body.Should().Contain("TWITCH_CLIENT_SECRET_MISSING");
    }

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
    public async Task Given_WorkflowRuleEndpoints_When_RuleIsUpdated_Then_CreatedAtIsPreserved()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var createResponse = await client.PostAsJsonAsync(
            "/api/rules",
            ValidRule("Preserved", actions: [new { type = "sendChatMessage", template = "first" }]),
            TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();
        var location = createResponse.Headers.Location!.ToString();
        var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var createdAt = created.RootElement.GetProperty("createdAt").GetDateTimeOffset();

        await Task.Delay(20, TestContext.Current.CancellationToken);
        var updateResponse = await client.PutAsJsonAsync(
            location,
            ValidRule("Updated", actions: [new { type = "sendChatMessage", template = "second" }]),
            TestContext.Current.CancellationToken);

        var body = await updateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, "response body was {0}", body);
        var updated = JsonDocument.Parse(body);
        updated.RootElement.GetProperty("createdAt").GetDateTimeOffset().Should().Be(createdAt);
    }

    [Fact]
    public async Task Given_WorkflowRuleCreate_When_ClientProvidesId_Then_StableErrorCodeIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/rules",
            ValidRule("Client id", id: "client-id"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("WORKFLOW_RULE_ID_NOT_ALLOWED");
    }

    [Theory]
    [InlineData("platform.connection_changed", null, "UNKNOWN_EVENT_TYPE_KEY")]
    [InlineData("user.message", "unknownAction", "UNKNOWN_ACTION_TYPE")]
    [InlineData("user.message", "missingTemplate", "ACTION_MISSING_REQUIRED_PARAM")]
    [InlineData("user.message", "invalidActionConfig", "INVALID_ACTION_CONFIG")]
    public async Task Given_WorkflowRuleValidation_When_InvalidActionRequestIsSubmitted_Then_ErrorCodeIsReturned(
        string eventTypeKey,
        string? actionCase,
        string expectedError)
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/rules",
            ValidRule("Bad rule", eventTypeKey: eventTypeKey, actions: InvalidActions(actionCase)),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain(expectedError);
    }

    [Fact]
    public async Task Given_WorkflowRuleValidation_When_InvalidConditionIsSubmitted_Then_ErrorCodeIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/rules",
            ValidRule("Bad condition", conditions: [new { type = "unknownCondition" }]),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("UNKNOWN_CONDITION_TYPE");
    }

    [Fact]
    public async Task Given_WorkflowRuleValidation_When_InvalidRegexIsSubmitted_Then_ErrorCodeIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/rules",
            ValidRule("Bad regex", conditions: [new { type = "messageContent", matchMode = "FullRegex", pattern = "[" }]),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("INVALID_REGEX_PATTERN");
    }

    [Fact]
    public async Task Given_WorkflowRuleValidation_When_ActionPayloadCannotDeserialize_Then_InvalidActionConfigIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/rules",
            ValidRule("Bad payload", actions: [new { type = "sendChatMessage", template = new { nested = true } }]),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("INVALID_ACTION_CONFIG");
    }

    [Fact]
    public async Task Given_WorkflowRuleValidation_When_PathAndBodyIdsDiffer_Then_ErrorCodeIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var createResponse = await client.PostAsJsonAsync("/api/rules", ValidRule("Rule"), TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync(
            createResponse.Headers.Location!.ToString(),
            ValidRule("Mismatch", id: "different"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("INVALID_RULE_ID_MISMATCH");
    }

    [Fact]
    public async Task Given_WorkflowRuleValidation_When_RuleCreatesMultiHopCycle_Then_CircularReferenceIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);
        var a = await CreateRuleAsync(client, "A", []);
        var b = await CreateRuleAsync(client, "B", [new { type = "invokeSubWorkflow", workflowId = a.Id }]);

        var response = await client.PutAsJsonAsync(
            a.Location,
            ValidRule("A updated", actions: [new { type = "invokeSubWorkflow", workflowId = b.Id }]),
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

    [Theory]
    [InlineData("/api/config/security.foo", "CONFIG_KEY_SECURITY_NAMESPACE")]
    [InlineData("/api/config/SECURITY.foo", "CONFIG_KEY_SECURITY_NAMESPACE")]
    [InlineData("/api/config/unknown.key", "UNKNOWN_CONFIG_KEY")]
    public async Task Given_ConfigEndpoint_When_KeyIsRejected_Then_ExpectedErrorCodeIsReturned(string path, string expectedError)
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(path.Contains("unknown", StringComparison.Ordinal)
            ? HttpStatusCode.BadRequest
            : HttpStatusCode.Forbidden);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain(expectedError);
    }

    [Fact]
    public async Task Given_ConfigEndpoint_When_KeyCaseDiffers_Then_ResponseUsesCanonicalKey()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/config/LOG.MIN_LEVEL", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("\"key\":\"log.min_level\"");
    }

    [Fact]
    public async Task Given_MemberEndpoint_When_InvalidQueryParamIsSubmitted_Then_ErrorCodeIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/members?limit=201", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("INVALID_QUERY_PARAM");
    }

    [Fact]
    public async Task Given_MemberEndpoint_When_OffsetExceedsCap_Then_ErrorCodeIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.GetAsync("/api/members?offset=1000001", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("INVALID_QUERY_PARAM");
    }

    [Fact]
    public async Task Given_SimulateEndpoint_When_UnknownAliasIsSubmitted_Then_ErrorCodeIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/user.message",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("UNKNOWN_SIMULATE_EVENT_TYPE");
    }

    [Fact]
    public async Task Given_SimulateEndpoint_When_RolesUseLegacyNumericFlags_Then_RequestIsAccepted()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/follow",
            new { roles = 1 },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Given_SimulateEndpoint_When_Posted_Then_AdapterDoesNotStartPerRequest()
    {
        await using var app = await StartAppAsync(["Simulation:Enabled", "false"]);
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/chat",
            new { message = "no start" },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Given_SimulateEndpoint_When_RolesUseStringNames_Then_RequestIsAccepted()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/follow",
            new { roles = new[] { "subscriber", "moderator" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Given_SimulateEndpoint_When_RoleNameIsUnknown_Then_InvalidQueryParamIsReturned()
    {
        await using var app = await StartAppAsync();
        using var client = CreateClient(app);

        var response = await client.PostAsJsonAsync(
            "/api/simulate/follow",
            new { roles = new[] { "not-a-role" } },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        json.Should().Contain("INVALID_QUERY_PARAM");
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

    [Fact]
    public void Given_DefaultLoopbackPorts_When_AllPairsAreUnavailable_Then_HostStartupThrowsPortExhaustedException()
    {
        var options = new PortAllocationOptions();
        var occupiedPorts = OccupyPorts(Enumerable
            .Range(0, ((options.LastApiPort - options.FirstApiPort) / options.PortStep) + 1)
            .Select(index => options.FirstApiPort + (index * options.PortStep)));
        try
        {
            var act = () => VulperonexWebApplication.CreateBuilder(
                new WebApplicationOptions { EnvironmentName = "Development" },
                configureDefaultLoopbackPorts: true);

            act.Should().Throw<Vulperonex.Web.Ports.PortExhaustedException>();
        }
        finally
        {
            foreach (var listener in occupiedPorts)
            {
                listener.Stop();
            }
        }
    }

    [Fact]
    public void Given_NoDatabasePathConfigured_When_Resolved_Then_PathIsUnderAppContextBaseDirectory()
    {
        var configuration = new ConfigurationBuilder().Build();

        var path = new DatabasePathResolver(configuration).Resolve();

        path.Should().Be(Path.Combine(AppContext.BaseDirectory, "vulperonex.db"));
    }

    [Fact]
    public async Task Given_WorkflowRuleRepository_When_StaleVersionUpdates_Then_ConcurrencyExceptionIsThrown()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        try
        {
            await using (var app = await StartAppAsync(["Database:Path", databasePath]))
            {
                await using var scope = app.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<VulperonexDbContext>();
                var repository = new WorkflowRuleRepository(context);
                var rule = new WorkflowRule
                {
                    Id = "concurrency",
                    Name = "Original",
                    EventTypeKey = "user.message",
                    Actions = [new Vulperonex.Application.Workflows.Actions.SendChatMessageAction { Template = "hi" }],
                };
                await repository.AddAsync(rule, TestContext.Current.CancellationToken);

                var first = rule with { Name = "First", Version = 0 };
                var second = rule with { Name = "Second", Version = 0 };
                await repository.UpdateAsync(first, 0, TestContext.Current.CancellationToken);

                var act = () => repository.UpdateAsync(second, 0, TestContext.Current.CancellationToken);

                await act.Should().ThrowAsync<WorkflowRuleConcurrencyException>();
            }
        }
        finally
        {
            DeleteSqliteFiles(databasePath);
        }
    }

    private static Task<WebApplication> StartAppAsync(params string?[] configurationPairs)
    {
        return StartAppAsync(configurationPairs, configureServices: null, manualMigration: true);
    }

    private static Task<WebApplication> StartAppAsync(string?[] configurationPairs, Action<IServiceCollection> configureServices)
    {
        return StartAppAsync(configurationPairs, configureServices, manualMigration: true);
    }

    private static async Task<WebApplication> StartAppAsync(
        string?[]? configurationPairs = null,
        Action<IServiceCollection>? configureServices = null,
        bool manualMigration = true)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var builder = VulperonexWebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = "Development",
            },
            configureDefaultLoopbackPorts: false);
        var configuration = new Dictionary<string, string?>
        {
            ["Database:Path"] = databasePath,
            ["Security:RootPath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        };
        configurationPairs ??= [];
        for (var index = 0; index < configurationPairs.Length; index += 2)
        {
            configuration[configurationPairs[index]!] = configurationPairs[index + 1];
        }

        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddSingleton<IFileSystem, TestFileSystem>();
        configureServices?.Invoke(builder.Services);
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://127.0.0.1:{GetAvailablePort()}");

        var app = VulperonexWebApplication.Build(builder);
        if (manualMigration)
        {
            await using var scope = app.Services.CreateAsyncScope();
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

    private static object ValidRule(
        string name,
        string eventTypeKey = "user.message",
        string? id = null,
        object[]? conditions = null,
        object[]? actions = null)
    {
        return new
        {
            id,
            name,
            eventTypeKey,
            isEnabled = true,
            priority = 0,
            conditions = conditions ?? [],
            actions = actions ?? [new { type = "sendChatMessage", template = "hi" }],
            executionMode = "Serial",
            maxParallelism = 1,
        };
    }

    private static object[] InvalidActions(string? actionCase)
    {
        return actionCase switch
        {
            null => [],
            "unknownAction" => [new { type = "unknownAction" }],
            "missingTemplate" => [new { type = "sendChatMessage" }],
            "invalidActionConfig" => [new { type = "sendChatMessage", template = "hi", timeoutMs = -1 }],
            _ => throw new ArgumentOutOfRangeException(nameof(actionCase), actionCase, null),
        };
    }

    private static async Task<(string Id, string Location)> CreateRuleAsync(HttpClient client, string name, object[] actions)
    {
        var response = await client.PostAsJsonAsync(
            "/api/rules",
            ValidRule(name, actions: actions.Length == 0 ? [new { type = "sendChatMessage", template = name }] : actions),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var location = response.Headers.Location!.ToString();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return (document.RootElement.GetProperty("id").GetString()!, location);
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static List<TcpListener> OccupyPorts(IEnumerable<int> ports)
    {
        var listeners = new List<TcpListener>();
        try
        {
            foreach (var port in ports)
            {
                var listener = new TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listeners.Add(listener);
            }

            return listeners;
        }
        catch
        {
            foreach (var listener in listeners)
            {
                listener.Stop();
            }

            throw;
        }
    }

    private static void DeleteSqliteFiles(string databasePath)
    {
        foreach (var path in new[] { databasePath, $"{databasePath}-shm", $"{databasePath}-wal" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // SQLite may keep handles briefly after host disposal on Windows; best-effort cleanup is enough here.
            }
        }
    }

    private sealed class RecordingTwitchTokenEndpoint : ITwitchTokenEndpoint
    {
        public string? ExchangeCode { get; private set; }
        public string? ExchangeVerifier { get; private set; }

        public Task<TwitchTokenResponse> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            CancellationToken cancellationToken = default)
        {
            ExchangeCode = code;
            ExchangeVerifier = codeVerifier;
            return Task.FromResult(new TwitchTokenResponse("access-1", "refresh-1"));
        }

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TwitchTokenResponse("access-1", "refresh-1"));
        }
    }

    private sealed class FailingTwitchTokenEndpoint : ITwitchTokenEndpoint
    {
        public Task<TwitchTokenResponse> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            CancellationToken cancellationToken = default)
        {
            throw new TwitchTokenExchangeException(HttpStatusCode.BadRequest);
        }

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            throw new TwitchTokenExchangeException(HttpStatusCode.BadRequest);
        }
    }

    private sealed class MissingSecretTwitchTokenEndpoint : ITwitchTokenEndpoint
    {
        public Task<TwitchTokenResponse> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            CancellationToken cancellationToken = default)
        {
            throw new TwitchClientSecretMissingException();
        }

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            throw new TwitchClientSecretMissingException();
        }
    }

    private sealed class TestFileSystem : IFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);

        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public void ApplyUserOnlyPermissions(string path)
        {
        }
    }
}
