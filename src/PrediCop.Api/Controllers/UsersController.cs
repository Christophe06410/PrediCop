using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class UsersController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetAll(CancellationToken ct)
    {
        var users = await db.Users
            .Where(u => u.TenantId == TenantId)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(ct);

        return Ok(users.Select(MapToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        return user is null ? NotFound() : Ok(MapToResponse(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.TenantId == TenantId && u.Email == req.Email, ct))
            return Problem(title: "Email déjà utilisé", statusCode: 409);

        if (await db.Users.AnyAsync(u => u.TenantId == TenantId && u.BadgeNumber == req.BadgeNumber, ct))
            return Problem(title: "Numéro de badge déjà utilisé", statusCode: 409);

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return Problem(title: "Rôle invalide", statusCode: 400);

        var user = new User
        {
            TenantId = TenantId,
            FirstName = req.FirstName,
            LastName = req.LastName,
            Email = req.Email,
            BadgeNumber = req.BadgeNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = role,
            IsActive = req.IsActive
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, MapToResponse(user));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();

        if (await db.Users.AnyAsync(u => u.TenantId == TenantId && u.Email == req.Email && u.Id != id, ct))
            return Problem(title: "Email déjà utilisé", statusCode: 409);

        if (!Enum.TryParse<UserRole>(req.Role, out var role))
            return Problem(title: "Rôle invalide", statusCode: 400);

        user.FirstName = req.FirstName;
        user.LastName = req.LastName;
        user.Email = req.Email;
        user.BadgeNumber = req.BadgeNumber;
        user.Role = role;
        user.IsActive = req.IsActive;

        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == TenantId, ct);
        if (user is null) return NotFound();

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static UserResponse MapToResponse(User u) => new()
    {
        Id = u.Id,
        FirstName = u.FirstName,
        LastName = u.LastName,
        FullName = u.FullName,
        Email = u.Email,
        BadgeNumber = u.BadgeNumber,
        Role = u.Role,
        TenantId = u.TenantId,
        IsActive = u.IsActive
    };
}

public record CreateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string BadgeNumber,
    string Password,
    string Role,
    bool IsActive = true);

public record UpdateUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string BadgeNumber,
    string Role,
    bool IsActive,
    string? Password = null);
