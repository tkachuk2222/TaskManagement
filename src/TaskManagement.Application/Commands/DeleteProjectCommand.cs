using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;

namespace TaskManagement.Application.Commands;

public record DeleteProjectCommand(
    string Id,
    string UserId
) : IRequest<Result>;

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Result>
{
    private readonly IProjectRepository _projectRepository;

    public DeleteProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Result> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var deleted = await _projectRepository.DeleteAsync(request.Id, request.UserId, cancellationToken);
        
        return deleted 
            ? Result.Success() 
            : Result.Failure("Project not found or already deleted");
    }
}
