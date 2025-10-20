using TaskManagement.Domain.Common;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;
using TaskPriority = TaskManagement.Domain.Enums.TaskPriority;

namespace TaskManagement.Domain.Entities;

public class ProjectTask : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string ProjectId { get; set; } = null!;
    public TaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public string? AssignedToId { get; set; }
    public string CreatedById { get; set; } = null!;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int EstimatedHours { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<string> Attachments { get; set; } = new();
}
