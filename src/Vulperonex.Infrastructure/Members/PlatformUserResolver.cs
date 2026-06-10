using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Vulperonex.Application.Members;
using Vulperonex.Application.Twitch;
using Vulperonex.Infrastructure.Data;

namespace Vulperonex.Infrastructure.Members;

/// <summary>
/// Resolves a chat-known identifier (login or display name, optionally <c>@</c>-prefixed,
/// including non-ASCII display names) to a single exact user. Mirrors the OmniCommander design:
/// known local users are matched first (exact login or display name); the Twitch API is only a
/// fallback — login via the users endpoint, display name via an exact channel search.
/// </summary>
public sealed partial class PlatformUserResolver(
    VulperonexDbContext context,
    IHelixClient helixClient) : IPlatformUserResolver
{
    public async Task<ResolvedPlatformUser> ResolveAsync(
        string platform,
        string input,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (input ?? string.Empty).Trim();
        var isDisplayNameSearch = trimmed.StartsWith('@');
        var clean = (isDisplayNameSearch ? trimmed[1..] : trimmed).Trim();

        if (string.IsNullOrWhiteSpace(clean))
        {
            return new ResolvedPlatformUser(string.Empty, string.Empty, null, false);
        }

        // A non-ASCII target (e.g. a Chinese display name) can never be a Twitch login, so it must
        // be resolved as a display name even without an explicit '@'.
        if (!isDisplayNameSearch && NonAsciiRegex().IsMatch(clean))
        {
            isDisplayNameSearch = true;
        }

        // 1. Local known users first — exact match keeps us from picking an unrelated same-named user.
        var local = await FindLocalAsync(platform, clean, isDisplayNameSearch, cancellationToken).ConfigureAwait(false);
        if (local is not null)
        {
            return local;
        }

        // 2. Platform API fallback: login -> users endpoint; display name -> exact channel search.
        var profile = isDisplayNameSearch
            ? await helixClient.SearchChannelExactAsync(clean, cancellationToken).ConfigureAwait(false)
            : await helixClient.LookupUserAsync(login: clean, userId: null, cancellationToken).ConfigureAwait(false);

        return profile is null
            ? new ResolvedPlatformUser(isDisplayNameSearch ? string.Empty : clean, clean, null, false)
            : new ResolvedPlatformUser(profile.Login, profile.DisplayName, profile.UserId, true);
    }

    private async Task<ResolvedPlatformUser?> FindLocalAsync(
        string platform,
        string clean,
        bool byDisplayName,
        CancellationToken cancellationToken)
    {
        var normalized = clean.ToLowerInvariant();
        var query = context.PlatformUserDisplayInfo
            .AsNoTracking()
            .Where(row => row.Platform == platform);

        var row = byDisplayName
            ? await FindUniqueDisplayNameMatchAsync(query, normalized, cancellationToken).ConfigureAwait(false)
            : await query.FirstOrDefaultAsync(
                r => r.Login != null && r.Login.ToLower() == normalized,
                cancellationToken).ConfigureAwait(false);

        // A local match is only useful if it carries a login (the key Shoutout needs). Otherwise
        // fall through to the API so we can resolve one.
        if (row is null || string.IsNullOrWhiteSpace(row.Login))
        {
            return null;
        }

        return new ResolvedPlatformUser(row.Login, row.DisplayName ?? row.Login, row.PlatformUserId, true);
    }

    private static async Task<Data.Entities.PlatformUserDisplayInfoEntity?> FindUniqueDisplayNameMatchAsync(
        IQueryable<Data.Entities.PlatformUserDisplayInfoEntity> query,
        string normalized,
        CancellationToken cancellationToken)
    {
        var matches = await query
            .Where(r => r.DisplayName != null && r.DisplayName.ToLower() == normalized)
            .Take(2)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return matches.Count == 1 ? matches[0] : null;
    }

    [GeneratedRegex(@"[^\x00-\x7F]")]
    private static partial Regex NonAsciiRegex();
}
