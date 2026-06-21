using System.ComponentModel.DataAnnotations;
using RestAPI.Models;
using TaskStatus = RestAPI.Models.TaskStatus;

namespace RestAPI.DTOs;

public record RegisterRequest(
    [Required][EmailAddress] string Email,
    [Required][MinLength(6)] string Password,
    [Required][MaxLength(100)] string FirstName,
    [Required][MaxLength(100)] string LastName
);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName
);

public record CreateProjectRequest(
    [Required][MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    DateTime? Deadline
);

public record UpdateProjectRequest(
    [Required][MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    DateTime? Deadline
);

public record ProjectDto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? Deadline,
    bool IsOverdue,
    UserDto Owner,
    bool IsOwner,
    int TaskCount
);

public record ProjectDetailDto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? Deadline,
    bool IsOverdue,
    UserDto Owner,
    bool IsOwner,
    IEnumerable<TaskDto> Tasks
);

public record CreateTaskRequest(
    [Required][MaxLength(300)] string Title,
    [MaxLength(2000)] string? Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTime? DueDate,
    string? AssignedUserId
);

public record UpdateTaskRequest(
    [Required][MaxLength(300)] string Title,
    [MaxLength(2000)] string? Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTime? DueDate,
    string? AssignedUserId
);

public record TaskDto(
    int Id,
    string Title,
    string? Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTime? DueDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ProjectId,
    string ProjectName,
    UserDto? AssignedUser
);
