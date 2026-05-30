using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Vulperonex.Adapters.Abstractions;
using Vulperonex.Adapters.Simulation;
using Vulperonex.Application.EventBus;
using Vulperonex.Application.Members;
using Vulperonex.Application.Modules;
using Vulperonex.Application.Settings;
using Vulperonex.Domain;
using Vulperonex.Domain.Events;
using Vulperonex.Domain.Members;
using Vulperonex.Web.Errors;
using Vulperonex.Web.Simulation;

namespace Vulperonex.Web.Endpoints;

public static class SimulateEndpoints
{
    public static IEndpointRouteBuilder MapSimulateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/simulate/checkin", async (
            HttpContext context,
            SimulateCheckInRequest request,
            IMemberResolver memberResolver,
            IMemberStreamStateRepository streamStateRepository,
            IMemberQueryService memberQueryService,
            IStreamEventBus eventBus,
            ISystemSettingsService systemSettingsService,
            IPlatformUserInfoCache userInfoCache,
            IModuleStateService modules,
            CancellationToken cancellationToken) =>
        {
            if (!await modules.IsEnabledAsync("checkin", cancellationToken).ConfigureAwait(false))
            {
                return ApiErrors.ToResult(ErrorCodes.ModuleDisabled, StatusCodes.Status503ServiceUnavailable);
            }

            var skipCooldown = request.SkipCooldown ?? false;
            var isTest = request.IsTest ?? false;

            var platform = "simulation";
            var userId = request.PlatformUserId ?? "sim-user";
            var displayName = request.DisplayName ?? "Sim User";
            var stampCount = request.StampCount ?? 1;

            var identity = PlatformIdentity.Create(platform, userId);

            int count;
            int totalLoyalty;
            if (isTest)
            {
                // TEST MODE: skip ALL persistence (member resolve / increment / member fetch).
                // Synthesise a count from any existing record (read-only) + requested stamps so
                // overlay preview reacts visually, without writing to DB.
                var existing = await memberQueryService.FindByIdentityAsync(identity, cancellationToken);
                var baseCount = existing?.Loyalty.CheckInCount ?? 0;
                count = baseCount + stampCount;
                totalLoyalty = (int)(existing?.Loyalty.TotalLoyalty ?? 0L);
            }
            else
            {
                await memberResolver.ResolveMemberIdAsync(identity, cancellationToken);

                count = 0;
                for (var i = 0; i < stampCount; i++)
                {
                    count = await streamStateRepository.IncrementCheckInAsync(identity, cancellationToken);
                }

                var member = await memberQueryService.FindByIdentityAsync(identity, cancellationToken)
                    ?? throw new InvalidOperationException($"Member '{platform}:{userId}' was not found after simulated check-in.");
                totalLoyalty = (int)member.Loyalty.TotalLoyalty;
            }

            var stampsPerRound = await systemSettingsService.GetAsync<int>("overlay.member.stamps_per_round", 10, cancellationToken);
            if (stampsPerRound <= 0) stampsPerRound = 10;

            var roundIndex = (int)Math.Ceiling((double)count / stampsPerRound);
            var stampSlotInRound = ((count - 1) % stampsPerRound) + 1;

            var streamRole = StreamRole.None;
            var displayInfo = await userInfoCache.GetAsync(platform, userId, cancellationToken);
            string? avatarUrl = null;
            if (displayInfo != null)
            {
                displayName = displayInfo.DisplayName ?? displayName;
                avatarUrl = displayInfo.AvatarUrl;
                if (displayInfo.IsSubscriber) streamRole |= StreamRole.Subscriber;
            }

            var streamUser = new StreamUser(platform, userId, displayName, streamRole);

            var checkInEvent = new MemberCheckedInEvent
            {
                Platform = platform,
                User = streamUser,
                AvatarUrl = avatarUrl,
                CheckInCount = count,
                TotalLoyalty = totalLoyalty,
                RoundIndex = roundIndex,
                StampSlotInRound = stampSlotInRound,
                SkipCooldown = skipCooldown
            };

            await eventBus.PublishAsync(checkInEvent, cancellationToken);

            return Results.Accepted(
                $"/api/simulate/events/{checkInEvent.EventId}",
                new SimulateResponse(
                    true,
                    checkInEvent.EventTypeKey,
                    checkInEvent.EventId,
                    checkInEvent.Platform,
                    checkInEvent.User?.UserId,
                    checkInEvent.User?.DisplayName,
                    checkInEvent.OccurredAt));
        });

        endpoints.MapPost("/api/simulate/{alias}", async (
            HttpContext context,
            string alias,
            SimulateRequest request,
            SimulationAliasRegistry aliases,
            ISimulationAdapter adapter,
            IPlatformUserInfoCache userInfoCache,
            CancellationToken cancellationToken) =>
        {
            var resolved = aliases.Find(alias);
            if (resolved is null)
            {
                return ApiErrors.ToResult(ErrorCodes.UnknownSimulateEventType, StatusCodes.Status400BadRequest);
            }

            var simulationRequest = ToSimulationRequest(resolved.Kind, request);
            if (simulationRequest is null)
            {
                return ApiErrors.ToResult(ErrorCodes.InvalidQueryParam, StatusCodes.Status400BadRequest);
            }

            await UpsertSimDisplayInfoAsync(userInfoCache, simulationRequest, request, cancellationToken);

            var streamEvent = await adapter.SimulateAsync(simulationRequest, cancellationToken);
            return Results.Accepted(
                $"/api/simulate/events/{streamEvent.EventId}",
                new SimulateResponse(
                    true,
                    streamEvent.EventTypeKey,
                    streamEvent.EventId,
                    streamEvent.Platform,
                    streamEvent.User?.UserId,
                    streamEvent.User?.DisplayName,
                    streamEvent.OccurredAt));
        });

        return endpoints;
    }



    private static async Task UpsertSimDisplayInfoAsync(
        IPlatformUserInfoCache cache,
        SimulationRequest simulationRequest,
        SimulateRequest payload,
        CancellationToken cancellationToken)
    {
        var badges = NormalizeBadgeKeys(payload.Badges);
        var colorHex = string.IsNullOrWhiteSpace(payload.ColorHex) ? null : payload.ColorHex.Trim();

        if (badges.Count == 0 && colorHex is null)
        {
            return;
        }

        await cache.UpdateAsync(
            simulationRequest.Platform,
            simulationRequest.User.UserId,
            current => current with
            {
                DisplayName = simulationRequest.User.DisplayName,
                ColorHex = colorHex ?? current.ColorHex,
                Badges = badges.Count > 0 ? badges : current.Badges,
                FetchedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);
    }

    private static IReadOnlyCollection<string> NormalizeBadgeKeys(JsonElement? badges)
    {
        if (badges is null || badges.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (badges.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var item in badges.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var raw = item.GetString();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            // Storage uses '_' separator to align with Helix descriptor Key format.
            result.Add(raw.Trim().Replace('/', '_'));
        }

        return result;
    }

    private static SimulationRequest? ToSimulationRequest(SimulationKind kind, SimulateRequest request)
    {
        var roles = ToRoles(request.Roles);
        if (roles is null)
        {
            return null;
        }

        var user = new StreamUser(
            "simulation",
            request.PlatformUserId ?? "sim-user",
            request.DisplayName ?? "Sim User",
            roles.Value);

        return kind switch
        {
            SimulationKind.Message => SimulationRequest.Message("simulation", user, request.Message ?? string.Empty),
            SimulationKind.Followed => SimulationRequest.Followed("simulation", user),
            SimulationKind.Subscribed => SimulationRequest.Subscribed("simulation", user, request.Tier ?? "1000"),
            SimulationKind.Donated => SimulationRequest.Donated("simulation", user, request.Bits ?? 100),
            SimulationKind.GiftedSubscription => SimulationRequest.GiftedSubscription("simulation", user, request.Tier ?? "1000", 1),
            SimulationKind.Raided => SimulationRequest.Raided("simulation", user, 100),
            SimulationKind.RewardRedeemed => SimulationRequest.RewardRedeemed("simulation", user, request.RewardId ?? "custom-reward", request.RewardId ?? "custom-reward"),
            _ => throw new NotSupportedException($"Unsupported simulate alias kind: {kind}."),
        };
    }

    private static StreamRole? ToRoles(JsonElement? roles)
    {
        if (roles is null || roles.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return StreamRole.None;
        }

        if (roles.Value.ValueKind == JsonValueKind.Number && roles.Value.TryGetInt32(out var numericRoles))
        {
            return Enum.IsDefined((StreamRole)numericRoles) || IsCompositeRoleValue(numericRoles)
                ? (StreamRole)numericRoles
                : null;
        }

        if (roles.Value.ValueKind == JsonValueKind.String)
        {
            return TryParseRole(roles.Value.GetString(), out var singleRole) ? singleRole : null;
        }

        if (roles.Value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var resolved = StreamRole.None;
        foreach (var role in roles.Value.EnumerateArray())
        {
            if (role.ValueKind != JsonValueKind.String || !TryParseRole(role.GetString(), out var parsed))
            {
                return null;
            }

            resolved |= parsed;
        }

        return resolved;
    }

    private static bool TryParseRole(string? role, out StreamRole parsed)
    {
        return Enum.TryParse(role, ignoreCase: true, out parsed)
            && Enum.IsDefined(parsed);
    }

    private static bool IsCompositeRoleValue(int numericRoles)
    {
        // Numeric legacy payloads may combine known role flags; unknown bits are rejected.
        const int allKnownFlags = (int)(StreamRole.Subscriber | StreamRole.Moderator | StreamRole.Vip | StreamRole.Follower | StreamRole.Broadcaster);
        return numericRoles >= 0 && (numericRoles & ~allKnownFlags) == 0;
    }

    private sealed record SimulateCheckInRequest(
        string? PlatformUserId,
        string? DisplayName,
        bool? SkipCooldown,
        int? StampCount,
        bool? IsTest);

    private sealed record SimulateRequest(
        string? PlatformUserId,
        string? DisplayName,
        JsonElement? Roles = null,
        string? Message = null,
        string? Tier = null,
        JsonElement? Badges = null,
        string? ColorHex = null,
        string? RecipientDisplayName = null,
        int? Bits = null,
        string? RewardId = null,
        string? UserInput = null);

    private sealed record SimulateResponse(
        bool Accepted,
        string EventTypeKey,
        string EventId,
        string Platform,
        string? PlatformUserId,
        string? DisplayName,
        DateTimeOffset OccurredAt);
}
