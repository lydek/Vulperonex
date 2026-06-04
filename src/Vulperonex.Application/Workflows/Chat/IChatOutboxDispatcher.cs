namespace Vulperonex.Application.Workflows.Chat;

public interface IChatOutboxDispatcher
{
    Task<int> DispatchOnceAsync(CancellationToken cancellationToken = default);

    Task DispatchItemAsync(Guid id, CancellationToken cancellationToken = default);
}
