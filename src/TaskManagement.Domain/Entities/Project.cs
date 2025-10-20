using TaskManagement.Domain.Common;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string OwnerId { get; set; } = null!;
    public ProjectStatus Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public List<string> MemberIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    
    // Navigation - not stored in DB, populated on query
    public List<ProjectTask>? Tasks { get; set; }
}
