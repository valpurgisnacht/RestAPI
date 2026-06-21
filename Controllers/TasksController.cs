using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestAPI.Data;
using RestAPI.DTOs;
using RestAPI.Models;

namespace RestAPI.Controllers;

[ApiController]
[Route("api/projects/{projectId:int}/tasks")]
[Authorize]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public TasksController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException();

    private async Task<(Project? projekt, bool maDoStep)> ZnajdzProjektAsync(int projektId)
    {
        var projekt = await _db.Projects
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == projektId);

        if (projekt is null)
            return (null, false);

        var userId = CurrentUserId;
        var maDoStep = projekt.OwnerId == userId
                    || projekt.Tasks.Any(t => t.AssignedUserId == userId);

        return (projekt, maDoStep);
    }

    private static TaskDto NaDto(ProjectTask t) => new(
        t.Id,
        t.Title,
        t.Description,
        t.Status,
        t.Priority,
        t.DueDate,
        t.CreatedAt,
        t.UpdatedAt,
        t.ProjectId,
        t.Project.Name,
        t.AssignedUser is null ? null
            : new UserDto(t.AssignedUser.Id, t.AssignedUser.Email!,
                          t.AssignedUser.FirstName, t.AssignedUser.LastName)
    );

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PobierzZadania(int projectId)
    {
        var (projekt, maDoStep) = await ZnajdzProjektAsync(projectId);
        if (projekt is null) return NotFound(new { message = "Projekt nie istnieje." });
        if (!maDoStep) return Forbid();

        var zadania = await _db.Tasks
            .Where(t => t.ProjectId == projectId)
            .Include(t => t.Project)
            .Include(t => t.AssignedUser)
            .AsNoTracking()
            .ToListAsync();

        return Ok(zadania.Select(NaDto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PobierzZadanie(int projectId, int id)
    {
        var (projekt, maDoStep) = await ZnajdzProjektAsync(projectId);
        if (projekt is null) return NotFound(new { message = "Projekt nie istnieje." });
        if (!maDoStep) return Forbid();

        var zadanie = await _db.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);

        if (zadanie is null)
            return NotFound(new { message = "Zadanie nie istnieje." });

        return Ok(NaDto(zadanie));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UtworzZadanie(int projectId, [FromBody] CreateTaskRequest request)
    {
        var userId = CurrentUserId;

        var projekt = await _db.Projects.FindAsync(projectId);
        if (projekt is null)
            return NotFound(new { message = "Projekt nie istnieje." });

        if (projekt.OwnerId != userId)
            return Forbid();

        if (projekt.IsOverdue)
            return BadRequest(new { message = "Nie można dodawać zadań — termin realizacji projektu już upłynął." });

        if (request.AssignedUserId is not null)
        {
            var przypisanyUser = await _db.Users.FindAsync(request.AssignedUserId);
            if (przypisanyUser is null)
                return BadRequest(new { message = "Wybrany użytkownik nie istnieje w systemie." });
        }

        var zadanie = new ProjectTask
        {
            Title = request.Title,
            Description = request.Description,
            Status = request.Status,
            Priority = request.Priority,
            DueDate = request.DueDate,
            ProjectId = projectId,
            AssignedUserId = request.AssignedUserId,
        };

        _db.Tasks.Add(zadanie);
        await _db.SaveChangesAsync();

        await _db.Entry(zadanie).Reference(t => t.Project).LoadAsync();
        await _db.Entry(zadanie).Reference(t => t.AssignedUser).LoadAsync();

        return CreatedAtAction(
            nameof(PobierzZadanie),
            new { projectId, id = zadanie.Id },
            NaDto(zadanie)
        );
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AktualizujZadanie(int projectId, int id, [FromBody] UpdateTaskRequest request)
    {
        var userId = CurrentUserId;

        var projekt = await _db.Projects.FindAsync(projectId);
        if (projekt is null)
            return NotFound(new { message = "Projekt nie istnieje." });

        var zadanie = await _db.Tasks
            .Include(t => t.Project)
            .Include(t => t.AssignedUser)
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);

        if (zadanie is null)
            return NotFound(new { message = "Zadanie nie istnieje." });

        bool jestWlascicielem = projekt.OwnerId == userId;
        bool jestPrzypisany = zadanie.AssignedUserId == userId;

        if (!jestWlascicielem && !jestPrzypisany)
            return Forbid();

        if (projekt.IsOverdue)
            return BadRequest(new { message = "Nie można edytować zadań — termin realizacji projektu już upłynął." });

        if (jestWlascicielem)
        {

            if (request.AssignedUserId is not null && request.AssignedUserId != zadanie.AssignedUserId)
            {
                var przypisanyUser = await _db.Users.FindAsync(request.AssignedUserId);
                if (przypisanyUser is null)
                    return BadRequest(new { message = "Wybrany użytkownik nie istnieje w systemie." });
            }

            zadanie.Title = request.Title;
            zadanie.Description = request.Description;
            zadanie.DueDate = request.DueDate;
            zadanie.AssignedUserId = request.AssignedUserId;
        }

        zadanie.Status = request.Status;
        zadanie.Priority = request.Priority;
        zadanie.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _db.Entry(zadanie).Reference(t => t.AssignedUser).LoadAsync();

        return Ok(NaDto(zadanie));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UsunZadanie(int projectId, int id)
    {
        var userId = CurrentUserId;

        var projekt = await _db.Projects.FindAsync(projectId);
        if (projekt is null)
            return NotFound(new { message = "Projekt nie istnieje." });

        if (projekt.OwnerId != userId)
            return Forbid();

        if (projekt.IsOverdue)
            return BadRequest(new { message = "Nie można usuwać zadań — termin realizacji projektu już upłynął." });

        var zadanie = await _db.Tasks
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);

        if (zadanie is null)
            return NotFound(new { message = "Zadanie nie istnieje." });

        _db.Tasks.Remove(zadanie);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
