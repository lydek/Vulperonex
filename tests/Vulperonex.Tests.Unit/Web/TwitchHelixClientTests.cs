using System.Net;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Vulperonex.Adapters.Twitch.Auth;
using Vulperonex.Application.Auth;
using Vulperonex.Web.TwitchAuth;
using Xunit;

namespace Vulperonex.Tests.Unit.Web;

public sealed class TwitchHelixClientTests
{
    [Fact]
    public async Task Given_LoginLookup_When_UserExists_Then_RequestUsesHelixHeadersAndMapsUser()
    {
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
                {
                  "data": [
                    {
                      "id": "user-1",
                      "login": "alice",
                      "display_name": "Alice",
                      "profile_image_url": "avatar",
                      "description": "desc",
                      "broadcaster_type": "affiliate"
                    }
                  ]
                }
                """);
        }))
        {
            BaseAddress = new Uri("https://api.twitch.tv/helix/"),
        };
        var client = new TwitchHelixClient(
            NewConfiguration(),
            NewTokenProvider(),
            httpClient);

        var user = await client.LookupUserAsync("alice", userId: null, TestContext.Current.CancellationToken);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be("https://api.twitch.tv/helix/users?login=alice");
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("access-1");
        capturedRequest.Headers.GetValues("Client-Id").Should().ContainSingle().Which.Should().Be("client-1");
        user.Should().BeEquivalentTo(new
        {
            UserId = "user-1",
            Login = "alice",
            DisplayName = "Alice",
            Avatar = "avatar",
            Description = "desc",
            IsAffiliate = true,
        });
    }

    [Fact]
    public async Task Given_UserIdLookup_When_UserMissing_Then_ReturnsNull()
    {
        using var httpClient = new HttpClient(new StubHandler(_ => JsonResponse("""{"data":[]}""")))
        {
            BaseAddress = new Uri("https://api.twitch.tv/helix/"),
        };
        var client = new TwitchHelixClient(
            NewConfiguration(),
            NewTokenProvider(),
            httpClient);

        var user = await client.LookupUserAsync(login: null, userId: "user-404", TestContext.Current.CancellationToken);

        user.Should().BeNull();
    }

    private static IConfiguration NewConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Twitch:ClientId"] = "client-1",
            })
            .Build();
    }

    private static TwitchAccessTokenProvider NewTokenProvider()
    {
        return new TwitchAccessTokenProvider(
            new RecordingTokenStore { RefreshToken = "refresh-1" },
            new RecordingTokenEndpoint());
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RecordingTokenStore : IOAuthTokenStore
    {
        public string? RefreshToken { get; init; }

        public Task StoreRefreshTokenAsync(string platform, string rawToken, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RefreshToken);
        }

        public Task<bool> HasRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(RefreshToken));
        }

        public Task ClearRefreshTokenAsync(string platform, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTokenEndpoint : ITwitchTokenEndpoint
    {
        public Task<TwitchTokenResponse> ExchangeCodeAsync(
            string code,
            string codeVerifier,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TwitchTokenResponse("access-1", "refresh-2"));
        }

        public Task<TwitchTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TwitchTokenResponse("access-1", "refresh-2"));
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
