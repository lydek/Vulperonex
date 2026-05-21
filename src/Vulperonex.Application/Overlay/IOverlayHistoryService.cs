namespace Vulperonex.Application.Overlay;

public interface IOverlayHistoryService<TPayload>
{
    string HubName { get; }

    int Capacity { get; }

    Task<IReadOnlyList<TPayload>> GetRecentAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TPayload payload, CancellationToken cancellationToken = default);

    Task ClearAllAsync(CancellationToken cancellationToken = default);
}

public sealed class OverlayHistoryOptions<TPayload>
{
    public required string HubName { get; init; }

    public required int DefaultCapacity { get; init; }

    public string DataSettingKey => $"overlay.history.{HubName}";

    public string CapacitySettingKey => $"overlay.history.cap.{HubName}";
}
