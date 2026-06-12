using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vulperonex.Infrastructure.Security;
using Vulperonex.Web.Middleware;
using Vulperonex.Web.Security;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

/// <summary>
/// Negative-path coverage for the loopback admin gate in
/// <see cref="AdminGuardMiddleware"/>: CSRF token validation, Origin/Referer
/// enforcement, host allow-listing, and URL-normalization bypass attempts.
/// The happy paths are exercised by every mutating integration test; these
/// cases pin down that the rejections actually reject.
/// </summary>
public sealed class AdminGuardMiddlewareLoopbackTests
{
    private static AdminCsrfTokenProvider CreateTokenProvider()
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.ContentRootPath.Returns(Path.GetTempPath());

        var fileSystem = Substitute.For<IFileSystem>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:CsrfTokenPath"] = Path.Combine(Path.GetTempPath(), $"csrf-{System.Guid.NewGuid():N}.token"),
            })
            .Build();

        return new AdminCsrfTokenProvider(
            environment,
            fileSystem,
            configuration,
            NullLogger<AdminCsrfTokenProvider>.Instance);
    }

    private static async Task<(int Status, bool NextRan)> InvokeAsync(
        AdminCsrfTokenProvider tokenProvider,
        string path,
        string method = "POST",
        string? csrfHeader = null,
        string? origin = null,
        string? referer = null,
        string host = "localhost")
    {
        var nextRan = false;
        var middleware = new AdminGuardMiddleware(_ =>
        {
            nextRan = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Host = new HostString(host);
        if (csrfHeader is not null)
        {
            context.Request.Headers["X-Admin-Csrf"] = csrfHeader;
        }

        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (referer is not null)
        {
            context.Request.Headers.Referer = referer;
        }

        await middleware.InvokeAsync(context, tokenProvider, new OverlayLanAccessKeyProvider());
        return (context.Response.StatusCode, nextRan);
    }

    [Fact]
    public async Task Given_MutatingApiRequest_When_CsrfHeaderMissing_Then_Rejected()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(tokenProvider, "/api/rules", origin: "http://localhost");

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Given_MutatingApiRequest_When_CsrfTokenIsWrong_Then_Rejected()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(
            tokenProvider,
            "/api/rules",
            csrfHeader: "definitely-not-the-token",
            origin: "http://localhost");

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Given_MutatingApiRequest_When_OriginAndRefererMissing_Then_Rejected()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(tokenProvider, "/api/rules", csrfHeader: tokenProvider.Token);

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Given_MutatingApiRequest_When_OriginIsForeign_Then_Rejected()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(
            tokenProvider,
            "/api/rules",
            csrfHeader: tokenProvider.Token,
            origin: "https://evil.example");

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Given_MutatingApiRequest_When_RefererIsForeign_Then_Rejected()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(
            tokenProvider,
            "/api/rules",
            csrfHeader: tokenProvider.Token,
            origin: "http://localhost",
            referer: "https://evil.example/page");

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Given_MutatingApiRequest_When_HostHeaderIsNotAllowListed_Then_Rejected()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(
            tokenProvider,
            "/api/rules",
            csrfHeader: tokenProvider.Token,
            origin: "http://rebound.example",
            host: "rebound.example");

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData("/api%2Foverlay%2Fpresets")]
    [InlineData("//api//overlay//presets")]
    public async Task Given_OverlayPathWithEncodedOrDoubledSlashes_When_NoCsrf_Then_NormalizationStillProtects(string path)
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(tokenProvider, path, method: "GET");

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Given_MutatingApiRequest_When_CsrfAndOriginAreValid_Then_Allowed()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(
            tokenProvider,
            "/api/rules",
            csrfHeader: tokenProvider.Token,
            origin: "http://localhost");

        nextRan.Should().BeTrue();
        status.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Given_ReadOnlyApiGet_When_NoCsrf_Then_Allowed()
    {
        var tokenProvider = CreateTokenProvider();

        var (status, nextRan) = await InvokeAsync(tokenProvider, "/api/rules", method: "GET");

        nextRan.Should().BeTrue();
        status.Should().Be(StatusCodes.Status200OK);
    }
}
