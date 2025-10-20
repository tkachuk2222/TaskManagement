using TaskManagement.Contracts.Common;

namespace TaskManagement.Application.Queries;

/// <summary>
/// Base interface for query handlers (read operations)
/// </summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for queries
/// </summary>
public interface IQuery<TResult>
{
}
