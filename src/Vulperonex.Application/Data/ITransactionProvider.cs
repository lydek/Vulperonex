using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vulperonex.Application.Data;

public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public interface ITransactionProvider
{
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
