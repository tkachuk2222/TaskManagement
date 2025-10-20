using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Application.Commands;
using TaskManagement.Application.Queries;
using TaskManagement.Contracts.Tasks;
using TaskManagement.API.Attributes;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.API.Controllers;

[ApiVersion("1.0")]
[Authorize]
[ApiController]
[Route("api/v{version:apiVersion}/projects/{projectId}/[controller]")]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        string projectId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TaskStatus? status = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        
        var query = new GetProjectTasksQuery(projectId, userId, pageNumber, pageSize, status, sortBy, sortDescending);
        var result = await _mediator.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpGet("{id}")]
    [ETag]
    public async Task<IActionResult> GetTask(
        string projectId,
        string id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var query = new GetTaskByIdQuery(id, userId);
        var result = await _mediator.Send(query, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(
        string projectId,
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var command = new CreateTaskCommand(projectId, userId, request);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [ValidateETag]
    [ETag]
    public async Task<IActionResult> UpdateTask(
        string projectId,
        string id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTaskCommand(id, request);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("{id}/complete")]
    [ValidateETag]
    [ETag]
    public async Task<IActionResult> CompleteTask(
        string projectId,
        string id,
        CancellationToken cancellationToken)
    {
        var request = new UpdateTaskStatusRequest { Status = TaskStatus.Done };
        var command = new UpdateTaskStatusCommand(id, request);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPatch("~/api/v{version:apiVersion}/tasks/{id}/status")]
    public async Task<IActionResult> UpdateTaskStatus(
        string id,
        [FromBody] UpdateTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTaskStatusCommand(id, request);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpPost("~/api/v{version:apiVersion}/tasks/{id}/assign")]
    public async Task<IActionResult> AssignTask(
        string id,
        [FromBody] AssignTaskRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AssignTaskCommand(id, request);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok(result.Data) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(
        string projectId,
        string id,
        CancellationToken cancellationToken)
    {
        var command = new DeleteTaskCommand(id);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result);
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value 
            ?? User.FindFirst("id")?.Value 
            ?? User.FindFirst("user_id")?.Value 
            ?? throw new UnauthorizedAccessException("User ID not found in claims");
    }
}
