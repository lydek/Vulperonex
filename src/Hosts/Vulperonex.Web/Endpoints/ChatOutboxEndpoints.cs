using Vulperonex.Application.Workflows.Chat;

namespace Vulperonex.Web.Endpoints;

public static class ChatOutboxEndpoints
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 500;

    public static IEndpointRouteBuilder MapChatOutboxEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chat-outbox");

        group.MapGet("/", async (
            IChatOutbox outbox,
            CancellationToken cancellationToken,
            string? status = null,
            string? platform = null,
            int? limit = null) =>
        {
            var snapshot = await outbox.SnapshotAsync(cancellationToken);
            var resolvedLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

            ChatOutboxItemStatus? parsedStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<ChatOutboxItemStatus>(status, ignoreCase: true, out var value))
                {
                    return Results.BadRequest(new { error = "INVALID_STATUS" });
                }
                parsedStatus = value;
            }

            var filtered = snapshot
                .Where(item => parsedStatus is null || item.Status == parsedStatus)
                .Where(item => string.IsNullOrWhiteSpace(platform)
                    || string.Equals(item.Platform, platform, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.EnqueuedAt)
                .Take(resolvedLimit)
                .Select(ToDto)
                .ToArray();

            return Results.Ok(filtered);
        });

        return endpoints;
    }

    private static ChatOutboxItemDto ToDto(ChatOutboxItem item)
    {
        return new ChatOutboxItemDto(
            item.Id,
            item.Platform,
            item.Channel,
            item.Message,
            item.DedupKey,
            item.EnqueuedAt,
            item.Status,
            item.ErrorMessage);
    }
}

public sealed record ChatOutboxItemDto(
    Guid Id,
    string Platform,
    string? Channel,
    string Message,
    string? DedupKey,
    DateTimeOffset EnqueuedAt,
    ChatOutboxItemStatus Status,
    string? ErrorMessage);
