using MediatR;
using TaskManagement.Application.Interfaces;
using TaskManagement.Contracts.Common;
using TaskManagement.Contracts.Projects;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Commands;

public record CreateProjectCommand(
    string UserId,
    CreateProjectRequest Request
) : IRequest<Result<ProjectResponse>>;

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Result<ProjectResponse>>
{
    private readonly IProjectRepository _projectRepository;

    public CreateProjectCommandHandler(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<Result<ProjectResponse>> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateCreateRequest(request.Request);
        if (validationErrors.Any())
            return Result<ProjectResponse>.ValidationFailure(validationErrors);

        var project = new Project
        {
            Name = request.Request.Name,
            Description = request.Request.Description,
            OwnerId = request.UserId,
            Status = request.Request.Status,
            StartDate = request.Request.StartDate,
            EndDate = request.Request.EndDate,
            Tags = request.Request.Tags,
            MemberIds = new List<string> { request.UserId }
        };

        var created = await _projectRepository.CreateAsync(project, cancellationToken);
        
        return Result<ProjectResponse>.Success(MapToProjectResponse(created, 0));
    }

    private static List<string> ValidateCreateRequest(CreateProjectRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Project name is required");
        else if (request.Name.Length > 200)
            errors.Add("Project name must not exceed 200 characters");

        if (request.Description?.Length > 2000)
            errors.Add("Description must not exceed 2000 characters");

        if (request.EndDate.HasValue && request.StartDate.HasValue && request.EndDate < request.StartDate)
            errors.Add("End date must be after start date");

        return errors;
    }

    private static ProjectResponse MapToProjectResponse(Project project, int taskCount)
    {
        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            OwnerId = project.OwnerId,
            Status = project.Status,
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            MemberIds = project.MemberIds,
            Tags = project.Tags,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            TaskCount = taskCount
        };
    }
}
