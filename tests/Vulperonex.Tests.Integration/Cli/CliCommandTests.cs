using System.Net;
using FluentAssertions;
using Xunit;

namespace Vulperonex.Tests.Integration.Cli;

public sealed class CliCommandTests
{
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
        using var client = new HttpClient(new StubHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("help must not call the API"))))
        {
            BaseAddress = new Uri("http://localhost"),
        };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await VulperonexCli.RunAsync(["help"], client, output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("rule - Manage workflow rules.");
        output.ToString().Should().Contain("twitch - Manage Twitch integration.");
        error.ToString().Should().BeEmpty();
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
