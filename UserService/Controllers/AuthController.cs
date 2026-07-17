using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserDbContext db, IJwtTokenGenerator tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await db.Users.AnyAsync(u => u.Email == req.Email))
            return Conflict(new { message = "Email already exists" });

        var user = new User
        {
            Email = req.Email,
            Name = req.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Created($"/api/users/{user.Id}", new { user.Id, user.Email, user.Name });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid credentials" });

        return Ok(new { token = tokens.Generate(user) });
    }
}

public record RegisterRequest(string Email, string Password, string Name);
public record LoginRequest(string Email, string Password);
