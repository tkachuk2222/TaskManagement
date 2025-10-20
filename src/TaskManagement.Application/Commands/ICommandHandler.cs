using TaskManagement.Contracts.Common;

namespace TaskManagement.Application.Commands;

/// <summary>
/// Base interface for command handlers (write operations)
/// </summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for commands without return value
/// </summary>
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for commands that return a result
/// </summary>
public interface ICommand<TResult>
{
}

/// <summary>
/// Marker interface for commands without return value
/// </summary>
public interface ICommand
{
}
