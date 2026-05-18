using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Vulperonex.Tests.Integration.Cli;

public sealed class CliCommandTests
{
    [Fact]
    public void Given_CliI18nManifest_When_Loaded_Then_SupportedCulturesHaveMatchingFiles()
    {
        var root = FindRepositoryRoot();
        var i18nDirectory = Path.Combine(root, "src", "Hosts", "Vulperonex.Cli", "Resources", "I18n");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(i18nDirectory, "manifest.json")));
        var defaultCulture = manifest.RootElement.GetProperty("defaultCulture").GetString();
        var supportedCultures = manifest.RootElement
            .GetProperty("supportedCultures")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();

        supportedCultures.Should().Contain(defaultCulture);
        foreach (var culture in supportedCultures)
        {
            File.Exists(Path.Combine(i18nDirectory, $"{culture}.json")).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Given_RuleListCommand_When_ApiReturnsJson_Then_ResponseIsWrittenToStdout()
    {
        using var client = new HttpClient(new StubHandler(request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri?.PathAndQuery.Should().Be("/api/rules");
            return JsonResponse(HttpStatusCode.OK, """[{"id":"rule-1"}]""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["rule", "list"], client, output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("rule-1");
        error.ToString().Should().BeEmpty();
    }

    [Theory]
    [InlineData(new[] { "rule", "show", "rule-1" }, "GET", "/api/rules/rule-1")]
    [InlineData(new[] { "rule", "enable", "rule-1" }, "PUT", "/api/rules/rule-1/enable")]
    [InlineData(new[] { "rule", "disable", "rule-1" }, "PUT", "/api/rules/rule-1/disable")]
    [InlineData(new[] { "rule", "delete", "rule-1" }, "DELETE", "/api/rules/rule-1")]
    [InlineData(new[] { "config", "get", "log.min_level" }, "GET", "/api/config/log.min_level")]
    [InlineData(new[] { "member", "list" }, "GET", "/api/members")]
    [InlineData(new[] { "member", "show", "user-1" }, "GET", "/api/members/user-1")]
    [InlineData(new[] { "simulate", "follow" }, "POST", "/api/simulate/follow")]
    [InlineData(new[] { "simulate", "sub" }, "POST", "/api/simulate/sub")]
    public async Task Given_CliCommand_When_Executed_Then_ExpectedApiRouteIsUsed(
        string[] args,
        string method,
        string path)
    {
        using var client = new HttpClient(new StubHandler(request =>
        {
            request.Method.Method.Should().Be(method);
            request.RequestUri?.PathAndQuery.Should().Be(path);
            return JsonResponse(HttpStatusCode.OK, """{}""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(args, client, output, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ConfigSetCommand_When_Executed_Then_ValueIsSentToApi()
    {
        using var client = new HttpClient(new StubHandler(async request =>
        {
            request.Method.Should().Be(HttpMethod.Put);
            request.RequestUri?.PathAndQuery.Should().Be("/api/config/log.min_level");
            var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.Should().Contain("\"value\":\"Debug\"");
            return JsonResponse(HttpStatusCode.OK, """{}""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["config", "set", "log.min_level", "Debug"], client, output, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ConfigCommand_When_ApiReturnsProtectedNamespaceError_Then_ErrorCodeIsWrittenToStderr()
    {
        using var client = new HttpClient(new StubHandler(request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri?.PathAndQuery.Should().Be("/api/config/oauth.twitch.refresh_token");
            return JsonResponse(HttpStatusCode.Forbidden, """{"error":"OAUTH_CREDENTIAL_NAMESPACE"}""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["config", "get", "oauth.twitch.refresh_token"], client, output, error);

        exitCode.Should().Be(1);
        output.ToString().Should().BeEmpty();
        error.ToString().Trim().Should().Be("OAUTH_CREDENTIAL_NAMESPACE");
    }

    [Fact]
    public async Task Given_SimulateChatCommand_When_MessageIsProvided_Then_ApiAliasIsInvoked()
    {
        using var client = new HttpClient(new StubHandler(async request =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/simulate/chat");
            var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.Should().Contain("hello from cli");
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new ByteArrayContent([]),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["simulate", "chat", "hello", "from", "cli"], client, TextWriter.Null, error);

        exitCode.Should().Be(0);
        error.ToString().Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwitchAuthStartCommand_When_ApiReturnsAuthorizeUrl_Then_UrlIsWrittenToStdout()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(async request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
                return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":true,"clientSecretConfigured":true,"hasRefreshToken":false}""");
            }

            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/start");
            var body = await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.Should().Contain("\"callbackPort\":7979");
            return JsonResponse(HttpStatusCode.OK, """{"authorizeUrl":"https://id.twitch.tv/oauth2/authorize?client_id=test","state":"state-1","callbackPort":7979}""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["twitch", "auth", "start", "--no-browser"], client, output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("https://id.twitch.tv/oauth2/authorize");
        output.ToString().Should().Contain("callbackPort");
        error.ToString().Should().BeEmpty();
        requestCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_TwitchAuthStartCommand_When_PublicClientIsConfigured_Then_DeviceFlowIsUsed()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
                return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":true,"clientSecretConfigured":false,"hasRefreshToken":false}""");
            }

            if (requestCount == 2)
            {
                request.Method.Should().Be(HttpMethod.Post);
                request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/device/start");
                return JsonResponse(HttpStatusCode.OK, """{"deviceCode":"device-1","userCode":"ABCD-EFGH","verificationUri":"https://www.twitch.tv/activate","expiresIn":30,"interval":1}""");
            }

            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/device/complete");
            return new HttpResponseMessage(HttpStatusCode.NoContent)
            {
                Content = new ByteArrayContent([]),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["twitch", "auth", "start", "--no-browser"], client, output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Twitch public-client authorization");
        output.ToString().Should().Contain("https://www.twitch.tv/activate");
        output.ToString().Should().Contain("ABCD-EFGH");
        output.ToString().Should().Contain("Twitch authorization completed.");
        error.ToString().Should().BeEmpty();
        requestCount.Should().Be(3);
    }

    [Fact]
    public async Task Given_HelpCommand_When_Executed_Then_CommandTreeIsWrittenWithoutCallingApi()
    {
        var previousLanguage = Environment.GetEnvironmentVariable("VULPERONEX_CLI_LANG");
        Environment.SetEnvironmentVariable("VULPERONEX_CLI_LANG", "en");
        using var client = new HttpClient(new StubHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("help must not call the API"))))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            var exitCode = await VulperonexCli.RunAsync(["help"], client, output, error);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("Vulperonex CLI Help");
            output.ToString().Should().Contain("[Workflow]");
            output.ToString().Should().Contain("rule/r");
            output.ToString().Should().Contain("Manage workflow rules. (list|show|enable|disable|delete)");
            output.ToString().Should().Contain("member/m/user/members");
            output.ToString().Should().Contain("twitch/tw");
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("VULPERONEX_CLI_LANG", previousLanguage);
        }
    }

    [Fact]
    public async Task Given_HelpCommand_When_LanguageIsChinese_Then_LocalizedHelpIsWritten()
    {
        var previousLanguage = Environment.GetEnvironmentVariable("VULPERONEX_CLI_LANG");
        Environment.SetEnvironmentVariable("VULPERONEX_CLI_LANG", "zh-TW");
        using var client = new HttpClient(new StubHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("help must not call the API"))))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            var exitCode = await VulperonexCli.RunAsync(["help"], client, output, error);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("Vulperonex CLI 說明");
            output.ToString().Should().Contain("[工作流程]");
            output.ToString().Should().Contain("管理工作流程規則。");
            output.ToString().Should().Contain("輸入命令群組名稱");
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("VULPERONEX_CLI_LANG", previousLanguage);
        }
    }

    [Fact]
    public async Task Given_CompositeCommandWithoutArgs_When_Executed_Then_SubcommandHelpIsWritten()
    {
        var previousLanguage = Environment.GetEnvironmentVariable("VULPERONEX_CLI_LANG");
        Environment.SetEnvironmentVariable("VULPERONEX_CLI_LANG", "en");
        using var client = new HttpClient(new StubHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("command help must not call the API"))))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            var exitCode = await VulperonexCli.RunAsync(["member"], client, output, error);

            exitCode.Should().Be(0);
            output.ToString().Should().Contain("member commands");
            output.ToString().Should().Contain("Usage: member <list|show>");
            output.ToString().Should().Contain("list/ls");
            output.ToString().Should().Contain("show/status/info/get/find");
            error.ToString().Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("VULPERONEX_CLI_LANG", previousLanguage);
        }
    }

    [Theory]
    [InlineData("ru", "rule ")]
    [InlineData("rule l", "rule list")]
    [InlineData("twitch a", "twitch auth ")]
    [InlineData("twitch auth st", "twitch auth start")]
    [InlineData("sim f", "simulate follow")]
    [InlineData("ex", "exit")]
    public void Given_CommandPrefix_When_TabCompletionRuns_Then_UniqueMatchIsCompleted(string input, string expected)
    {
        var completed = CommandCompletion.Complete(input, CommandTreeFactory.Create());

        completed.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("rule ")]
    public void Given_AmbiguousPrefix_When_TabCompletionRuns_Then_InputIsUnchanged(string input)
    {
        var completed = CommandCompletion.Complete(input, CommandTreeFactory.Create());

        completed.Should().Be(input);
    }

    [Fact]
    public void Given_AmbiguousPrefix_When_TabCompletionCycles_Then_CandidatesAdvance()
    {
        var dispatcher = CommandTreeFactory.Create();
        var cycle = CommandCompletion.StartCycle(string.Empty, dispatcher);

        var first = CommandCompletion.ApplyCycle(cycle, dispatcher, out var nextCycle);
        var second = CommandCompletion.ApplyCycle(nextCycle, dispatcher, out _);

        first.Should().Be("config ");
        second.Should().Be("exit");
    }

    [Fact]
    public async Task Given_NoArgs_When_StdinContainsCommands_Then_ReplDispatchesEachLineUntilExit()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
                return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":true,"clientSecretConfigured":true,"hasRefreshToken":true}""");
            }

            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri?.PathAndQuery.Should().Be("/api/rules");
            return JsonResponse(HttpStatusCode.OK, """[]""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var input = new StringReader("rule list\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync([], client, output, error, input);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Vulperonex CLI interactive mode");
        output.ToString().Should().Contain("API: http://localhost/");
        output.ToString().Should().Contain("vulperonex> ");
        output.ToString().Should().Contain("[]");
        error.ToString().Should().BeEmpty();
        requestCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_ReplCommand_When_ApiReturnsError_Then_StderrGetsCodeAndSessionContinues()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
                return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":true,"clientSecretConfigured":true,"hasRefreshToken":true}""");
            }

            if (requestCount == 2)
            {
                request.RequestUri?.PathAndQuery.Should().Be("/api/config/oauth.twitch.refresh_token");
                return JsonResponse(HttpStatusCode.Forbidden, """{"error":"OAUTH_CREDENTIAL_NAMESPACE"}""");
            }

            request.RequestUri?.PathAndQuery.Should().Be("/api/rules");
            return JsonResponse(HttpStatusCode.OK, """[]""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var input = new StringReader("config get oauth.twitch.refresh_token\nrule list\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync([], client, output, error, input);

        exitCode.Should().Be(0);
        error.ToString().Should().Contain("OAUTH_CREDENTIAL_NAMESPACE");
        output.ToString().Should().Contain("vulperonex> ");
        output.ToString().Should().Contain("[]");
        requestCount.Should().Be(3);
    }

    [Fact]
    public async Task Given_ReplStart_When_TwitchClientIdMissing_Then_NoTwitchModeBannerIsWritten()
    {
        using var client = new HttpClient(new StubHandler(request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
            return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":false,"clientSecretConfigured":false,"hasRefreshToken":false}""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var input = new StringReader("exit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync([], client, output, error, input);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Vulperonex CLI interactive mode");
        output.ToString().Should().Contain("vulperonex> ");
        error.ToString().Should().Contain("Twitch:ClientId is not set");
        error.ToString().Should().Contain("no-Twitch mode");
    }

    [Fact]
    public async Task Given_TwitchAuthStartInRepl_When_ClientIdMissing_Then_StartEndpointIsNotCalled()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
            return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":false,"clientSecretConfigured":false,"hasRefreshToken":false}""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var input = new StringReader("twitch auth start\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync([], client, output, error, input);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Vulperonex CLI interactive mode");
        error.ToString().Should().Contain("TWITCH_CLIENT_ID_MISSING");
        error.ToString().Should().Contain("non-Twitch commands");
        requestCount.Should().Be(2);
    }

    [Fact]
    public async Task Given_ReplCommand_When_ApiConnectionFails_Then_ErrorIsWrittenAndSessionContinues()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
                return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":true,"clientSecretConfigured":true,"hasRefreshToken":true}""");
            }

            if (requestCount == 2)
            {
                throw new HttpRequestException("api unavailable");
            }

            request.RequestUri?.PathAndQuery.Should().Be("/api/rules");
            return JsonResponse(HttpStatusCode.OK, """[]""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var input = new StringReader("member list\nrule list\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync([], client, output, error, input);

        exitCode.Should().Be(0);
        error.ToString().Should().Contain("HTTP_REQUEST_FAILED");
        output.ToString().Should().Contain("[]");
        requestCount.Should().Be(3);
    }

    [Fact]
    public async Task Given_ReplCommand_When_UnexpectedErrorIsThrown_Then_ErrorIsWrittenAndSessionContinues()
    {
        var requestCount = 0;
        using var client = new HttpClient(new StubHandler(request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                request.RequestUri?.PathAndQuery.Should().Be("/api/twitch/auth/status");
                return JsonResponse(HttpStatusCode.OK, """{"clientIdConfigured":true,"clientSecretConfigured":true,"hasRefreshToken":true}""");
            }

            if (requestCount == 2)
            {
                throw new InvalidOperationException("unexpected");
            }

            request.RequestUri?.PathAndQuery.Should().Be("/api/rules");
            return JsonResponse(HttpStatusCode.OK, """[]""");
        }))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var input = new StringReader("member list\nrule list\nexit\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync([], client, output, error, input);

        exitCode.Should().Be(0);
        error.ToString().Should().Contain("CLI_UNEXPECTED_ERROR");
        output.ToString().Should().Contain("[]");
        requestCount.Should().Be(3);
    }

    [Fact]
    public async Task Given_TwitchAuthStartInRepl_When_TokenIsCancelled_Then_OauthCancelledIsWrittenToStderr()
    {
        using var client = new HttpClient(new StubHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("API must not be hit after cancellation"))))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var context = new CliExecutionContext(client, output, error, jsonOptions, isInteractive: true);
        var dispatcher = CommandTreeFactory.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var exitCode = await dispatcher.DispatchAsync(["twitch", "auth", "start", "--no-browser"], context, cts.Token);

        exitCode.Should().Be(1);
        output.ToString().Should().BeEmpty();
        error.ToString().Trim().Should().Be("TWITCH_OAUTH_CANCELLED");
    }

    [Fact]
    public async Task Given_TwitchAuthStartOneShot_When_TokenIsCancelled_Then_OauthCancelledIsNotEmitted()
    {
        using var client = new HttpClient(new StubHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("API must not be hit after cancellation"))))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();
        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var context = new CliExecutionContext(client, output, error, jsonOptions, isInteractive: false);
        var dispatcher = CommandTreeFactory.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await dispatcher.DispatchAsync(["twitch", "auth", "start", "--no-browser"], context, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        error.ToString().Should().NotContain("TWITCH_OAUTH_CANCELLED");
    }

    [Fact]
    public async Task Given_InteractiveFlag_When_FollowedByExtraArgs_Then_InvalidArgsIsReturned()
    {
        using var client = new HttpClient(new StubHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("invalid args must not call the API"))))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["--interactive", "rule"], client, output, error, new StringReader(""));

        exitCode.Should().Be(1);
        output.ToString().Should().BeEmpty();
        error.ToString().Trim().Should().Be("INVALID_ARGS");
    }

    [Fact]
    public async Task Given_ApiUrlEnvironmentVariable_When_TargetIsNotLoopback_Then_CliRejectsIt()
    {
        var previous = Environment.GetEnvironmentVariable("VULPERONEX_API_URL");
        Environment.SetEnvironmentVariable("VULPERONEX_API_URL", "http://example.com:5000");
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            var exitCode = await VulperonexCli.RunAsync(["rule", "list"], output: output, error: error);

            exitCode.Should().Be(1);
            output.ToString().Should().BeEmpty();
            error.ToString().Trim().Should().Be("CLI_API_URL_NOT_LOOPBACK");
        }
        finally
        {
            Environment.SetEnvironmentVariable("VULPERONEX_API_URL", previous);
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulperonex.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this(request => Task.FromResult(handler(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return handler(request);
        }
    }
}
