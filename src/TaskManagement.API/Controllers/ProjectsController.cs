using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Commands;
using TaskManagement.Application.Queries;
using TaskManagement.Contracts.Projects;
using TaskManagement.API.Attributes;

namespace TaskManagement.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        
        var query = new GetUserProjectsQuery(userId, pageNumber, pageSize, search, status);
        var result = await _mediator.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{id}")]
    [ETag]
    public async Task<IActionResult> GetProject(string id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var query = new GetProjectByIdQuery(id, userId);
        var result = await _mediator.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var command = new CreateProjectCommand(userId, request);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess 
            ? CreatedAtAction(nameof(GetProject), new { id = result.Data!.Id }, result.Data)
            : BadRequest(result);
    }

    [HttpPut("{id}")]
    [ValidateETag]
    [ETag]
    public async Task<IActionResult> UpdateProject(
        string id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var command = new UpdateProjectCommand(id, userId, request);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    [ValidateETag]
    public async Task<IActionResult> DeleteProject(string id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var command = new DeleteProjectCommand(id, userId);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result);
    }

    [HttpGet("{id}/analytics")]
    public async Task<IActionResult> GetProjectAnalytics(string id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var query = new GetProjectAnalyticsQuery(id, userId);
        var result = await _mediator.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : NotFound(result);
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value 
            ?? User.FindFirst("id")?.Value 
            ?? User.FindFirst("user_id")?.Value 
            ?? throw new UnauthorizedAccessException("User ID not found in claims");
    }
}
