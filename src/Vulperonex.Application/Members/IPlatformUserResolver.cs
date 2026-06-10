namespace Vulperonex.Application.Members;

/// <summary>
/// Result of resolving a free-form target (login, @displayname, display name including
/// non-ASCII) to a single exact platform user.
/// </summary>
public sealed record ResolvedPlatformUser(string Login, string DisplayName, string? UserId, bool IsFound);

/// <summary>
/// Resolves the kind of identifier a streamer actually knows from chat — a login or a
/// display name (e.g. <c>viewer_login</c> or <c>@DisplayName</c>) — to a single exact user.
/// Local known-user data is preferred; the platform API is only a fallback, and matches are
/// always exact (never a fuzzy "many users with the same name" result).
/// </summary>
public interface IPlatformUserResolver
{
    Task<ResolvedPlatformUser> ResolveAsync(
        string platform,
        string input,
        CancellationToken cancellationToken = default);
}
