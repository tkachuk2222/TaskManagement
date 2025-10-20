using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;
using TaskPriority = TaskManagement.Domain.Enums.TaskPriority;

namespace TaskManagement.Contracts.Tasks;

public class CreateTaskRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public string? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public int EstimatedHours { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class UpdateTaskRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public TaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public string? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public int? EstimatedHours { get; set; }
    public List<string>? Tags { get; set; }
}

public class UpdateTaskStatusRequest
{
    public TaskStatus Status { get; set; }
}

public class AssignTaskRequest
{
    public string UserId { get; set; } = null!;
}

public class TaskDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public string? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int EstimatedHours { get; set; }
    public List<string> Tags { get; set; } = new();
    public string Id { get; set; }
    public string ProjectId { get; set; }
    public string CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
