using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestAPI.Models;

public enum TaskStatus
{
    Todo,        
    InProgress,  
    Done,        
    Cancelled   
}

public enum TaskPriority
{
    Low,    
    Medium,  
    High,    
    Critical  
}

public class ProjectTask
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int ProjectId { get; set; }

    public string? AssignedUserId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project Project { get; set; } = null!;

    [ForeignKey(nameof(AssignedUserId))]
    public ApplicationUser? AssignedUser { get; set; }
}
