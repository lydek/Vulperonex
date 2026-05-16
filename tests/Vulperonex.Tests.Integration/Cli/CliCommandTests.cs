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
