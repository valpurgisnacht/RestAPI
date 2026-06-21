using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RestAPI.DTOs;
using RestAPI.Models;
using RestAPI.Services;

namespace RestAPI.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthController(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
 
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return BadRequest(new { message = "Podany adres email jest już zajęty." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { bledy = result.Errors.Select(e => e.Description) });

        var (token, expiresAt) = _tokenService.GenerateToken(user);

        return CreatedAtAction(nameof(Register), new AuthResponse(
            token,
            expiresAt,
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName)
        ));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { message = "Nieprawidłowy adres email lub hasło." });

        var (token, expiresAt) = _tokenService.GenerateToken(user);

        return Ok(new AuthResponse(
            token,
            expiresAt,
            new UserDto(user.Id, user.Email!, user.FirstName, user.LastName)
        ));
    }
}
