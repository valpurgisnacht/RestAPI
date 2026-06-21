using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestAPI.Data;
using RestAPI.DTOs;
using RestAPI.Models;

namespace RestAPI.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
[Produces("application/json")]
public class ProjectsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProjectsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException();


    private IQueryable<Project> ProjektyUzytkownika(string userId) =>
        _db.Projects
           .Where(p => p.OwnerId == userId
                    || p.Tasks.Any(t => t.AssignedUserId == userId));

    private static ProjectDto NaDto(Project p, string aktualnyUserId) => new(
        p.Id,
        p.Name,
        p.Description,
        p.CreatedAt,
        p.UpdatedAt,
        p.Deadline,
        p.IsOverdue,
        new UserDto(p.Owner.Id, p.Owner.Email!, p.Owner.FirstName, p.Owner.LastName),
        p.OwnerId == aktualnyUserId,
        p.Tasks.Count
    );

    private static TaskDto ZadanieNaDto(ProjectTask t) => new(
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
    [ProducesResponseType(typeof(IEnumerable<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PobierzProjekty()
    {
        var userId = CurrentUserId;

        var projekty = await ProjektyUzytkownika(userId)
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
            .AsNoTracking()
            .ToListAsync();

        return Ok(projekty.Select(p => NaDto(p, userId)));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProjectDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PobierzProjekt(int id)
    {
        var userId = CurrentUserId;

        var projekt = await ProjektyUzytkownika(userId)
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
                .ThenInclude(t => t.AssignedUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (projekt is null)
            return NotFound(new { message = "Projekt nie istnieje lub nie masz do niego dostępu." });

        var dto = new ProjectDetailDto(
            projekt.Id,
            projekt.Name,
            projekt.Description,
            projekt.CreatedAt,
            projekt.UpdatedAt,
            projekt.Deadline,
            projekt.IsOverdue,
            new UserDto(projekt.Owner.Id, projekt.Owner.Email!,
                        projekt.Owner.FirstName, projekt.Owner.LastName),
            projekt.OwnerId == userId,
            projekt.Tasks.Select(ZadanieNaDto)
        );

        return Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UtworzProjekt([FromBody] CreateProjectRequest request)
    {
        var userId = CurrentUserId;

        // Termin musi być datą w przyszłości
        if (request.Deadline.HasValue && request.Deadline.Value <= DateTime.UtcNow)
            return BadRequest(new { message = "Termin zakończenia projektu musi być datą w przyszłości." });

        var projekt = new Project
        {
            Name = request.Name,
            Description = request.Description,
            Deadline = request.Deadline,
            OwnerId = userId,
        };

        _db.Projects.Add(projekt);
        await _db.SaveChangesAsync();

        await _db.Entry(projekt).Reference(p => p.Owner).LoadAsync();

        return CreatedAtAction(
            nameof(PobierzProjekt),
            new { id = projekt.Id },
            NaDto(projekt, userId)
        );
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AktualizujProjekt(int id, [FromBody] UpdateProjectRequest request)
    {
        var userId = CurrentUserId;

        var projekt = await _db.Projects
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (projekt is null)
            return NotFound(new { message = "Projekt nie istnieje." });

        if (projekt.OwnerId != userId)
            return Forbid();

        if (request.Deadline.HasValue && request.Deadline.Value <= DateTime.UtcNow)
            return BadRequest(new { message = "Termin zakończenia projektu musi być datą w przyszłości." });

        projekt.Name = request.Name;
        projekt.Description = request.Description;
        projekt.Deadline = request.Deadline;
        projekt.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(NaDto(projekt, userId));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UsunProjekt(int id)
    {
        var userId = CurrentUserId;

        var projekt = await _db.Projects.FindAsync(id);

        if (projekt is null)
            return NotFound(new { message = "Projekt nie istnieje." });

        if (projekt.OwnerId != userId)
            return Forbid();

        _db.Projects.Remove(projekt);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
