using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Vulperonex.Web.Middleware;
using Vulperonex.Web.Security;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

/// <summary>
/// Unit coverage for the non-loopback (LAN) gate added to <see cref="AdminGuardMiddleware"/>.
/// The CSRF token provider is never touched on the LAN branch, so it is passed as null.
/// </summary>
public sealed class AdminGuardMiddlewareLanTests
{
    private const string ValidKey = "lan-access-key";
    private static readonly IPAddress LanIp = IPAddress.Parse("192.168.1.50");

    private static async Task<(int status, bool nextRan)> Invoke(
        IPAddress remoteIp,
        string path,
        string method = "GET",
        string? key = null,
        string keyInProvider = ValidKey)
    {
        var nextRan = false;
        var middleware = new AdminGuardMiddleware(_ =>
        {
            nextRan = true;
            return Task.CompletedTask;
        });

        var keyProvider = new OverlayLanAccessKeyProvider();
        keyProvider.SetKey(keyInProvider);

        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        context.Connection.RemoteIpAddress = remoteIp;
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Host = new HostString("localhost");
        if (key is not null)
        {
            context.Request.QueryString = new QueryString($"?k={key}");
        }

        await middleware.InvokeAsync(context, tokenProvider: null!, keyProvider);
        return (context.Response.StatusCode, nextRan);
    }

    [Fact]
    public async Task Given_LanRequest_When_OverlayPageGetWithoutKey_Then_Allowed()
    {
        var (status, nextRan) = await Invoke(LanIp, "/overlay/chat");

        nextRan.Should().BeTrue();
        status.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Given_LanRequest_When_StaticAssetGet_Then_Allowed()
    {
        var (_, nextRan) = await Invoke(LanIp, "/assets/index-abc.js");

        nextRan.Should().BeTrue();
    }

    [Fact]
    public async Task Given_LanRequest_When_HubWithValidKey_Then_Allowed()
    {
        var (_, nextRan) = await Invoke(LanIp, "/hubs/overlay/chat", key: ValidKey);

        nextRan.Should().BeTrue();
    }

    [Fact]
    public async Task Given_LanRequest_When_HubWithoutKey_Then_Forbidden()
    {
        var (status, nextRan) = await Invoke(LanIp, "/hubs/overlay/chat");

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Given_LanRequest_When_HubWithWrongKey_Then_Forbidden()
    {
        var (status, _) = await Invoke(LanIp, "/hubs/overlay/chat", key: "nope");

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Given_LanRequest_When_AdminApi_Then_Forbidden()
    {
        var (status, nextRan) = await Invoke(LanIp, "/api/rules", key: ValidKey);

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Given_LanRequest_When_SensitiveConfigKey_Then_Forbidden()
    {
        var (status, nextRan) = await Invoke(LanIp, "/api/config/twitch.client_id", key: ValidKey);

        nextRan.Should().BeFalse();
        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Given_LanRequest_When_OverlaySafeConfigWithKey_Then_Allowed()
    {
        var (_, nextRan) = await Invoke(LanIp, "/api/config/overlay.chat.preset", key: ValidKey);

        nextRan.Should().BeTrue();
    }

    [Fact]
    public async Task Given_LanRequest_When_OverlaySafeConfigWithoutKey_Then_Forbidden()
    {
        var (status, _) = await Invoke(LanIp, "/api/config/overlay.chat.preset");

        status.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Given_LoopbackRequest_When_UnprotectedGet_Then_Allowed()
    {
        // Loopback path retains existing behaviour: a non-mutating, non-overlay GET passes untouched.
        var (_, nextRan) = await Invoke(IPAddress.Loopback, "/api/rules");

        nextRan.Should().BeTrue();
    }
}
