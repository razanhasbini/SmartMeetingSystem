using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartMeetingSystem.Domain.Entities;

namespace SmartMeetingSystem.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITokenService _tokenService;

    public AuthController(UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new AppUser { UserName = dto.UserName, Email = dto.Email, FullName = dto.FullName };
        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded) return BadRequest(result.Errors);

        await _userManager.AddToRoleAsync(user, "User");

        var (token, expires) = await _tokenService.CreateAccessTokenAsync(user);
        return Ok(new { token, expires });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByNameAsync(dto.UserNameOrEmail)
                   ?? await _userManager.FindByEmailAsync(dto.UserNameOrEmail);

        if (user == null) return Unauthorized();

        var check = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!check.Succeeded) return Unauthorized();

        var (token, expires) = await _tokenService.CreateAccessTokenAsync(user);
        return Ok(new { token, expires });
    }
}

public record RegisterDto(string UserName, string Email, string Password, string? FullName);
public record LoginDto(string UserNameOrEmail, string Password);
