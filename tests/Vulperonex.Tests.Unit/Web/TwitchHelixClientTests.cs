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
            new FakeSettingsService(),
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
            new FakeSettingsService(),
            httpClient);

        var user = await client.LookupUserAsync(login: null, userId: "user-404", TestContext.Current.CancellationToken);

        user.Should().BeNull();
    }

    [Fact]
    public async Task Given_Shoutout_When_TargetExists_Then_PostsHelixShoutoutRequest()
    {
        var requests = new List<HttpRequestMessage>();
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            requests.Add(request);
            return request.Method == HttpMethod.Get
                ? JsonResponse("""
                    {
                      "data": [
                        {
                          "id": "target-1",
                          "login": "alice",
                          "display_name": "Alice",
                          "profile_image_url": "avatar",
                          "description": "desc",
                          "broadcaster_type": ""
                        }
                      ]
                    }
                    """)
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://api.twitch.tv/helix/"),
        };
        var client = new TwitchHelixClient(
            NewConfiguration(),
            NewTokenProvider(),
            new FakeSettingsService(),
            httpClient);

        var result = await client.SendShoutoutAsync("alice", TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(new
        {
            IsSent = true,
            TargetLogin = "alice",
            TargetUserId = "target-1",
            TargetDisplayName = "Alice",
        });
        requests.Should().HaveCount(2);
        requests[1].Method.Should().Be(HttpMethod.Post);
        requests[1].RequestUri!.ToString().Should().Be(
            "https://api.twitch.tv/helix/chat/shoutouts?from_broadcaster_id=broadcaster-1&to_broadcaster_id=target-1&moderator_id=moderator-1");
    }

    [Fact]
    public async Task Given_RedemptionRefund_When_Called_Then_PatchesRedemptionStatusToCanceled()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        using var httpClient = new HttpClient(new StubHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }))
        {
            BaseAddress = new Uri("https://api.twitch.tv/helix/"),
        };
        var client = new TwitchHelixClient(
            NewConfiguration(),
            NewTokenProvider(),
            new FakeSettingsService(),
            httpClient);

        var refunded = await client.RefundRedemptionAsync("reward-1", "redemption-1", TestContext.Current.CancellationToken);

        refunded.Should().BeTrue();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Patch);
        capturedRequest.RequestUri!.ToString().Should().Be(
            "https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions?broadcaster_id=broadcaster-1&reward_id=reward-1&id=redemption-1");
        capturedBody.Should().Contain("\"status\":\"CANCELED\"");
    }

    private static IConfiguration NewConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Twitch:ClientId"] = "client-1",
                ["Twitch:BroadcasterId"] = "broadcaster-1",
                ["Twitch:ModeratorId"] = "moderator-1",
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

    private sealed class FakeSettingsService : Vulperonex.Application.Settings.ISystemSettingsService
    {
        public IObservable<Vulperonex.Application.Settings.SettingChangedEvent> Changes => throw new NotImplementedException();

        public Task<T> GetAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(defaultValue);
        }

        public Task SetAsync<T>(string key, T value, string category, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
