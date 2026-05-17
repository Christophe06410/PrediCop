using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrediCop.Core.DTOs;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive && !u.IsDeleted, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Problem(title: "Identifiants invalides", statusCode: 401);

        // Resolve the vehicle this officer is currently assigned to (used for mission filtering on mobile)
        var vehicleId = await db.VehicleOfficers
            .Where(vo => vo.UserId == user.Id && vo.IsActive)
            .Select(vo => (Guid?)vo.VehicleId)
            .FirstOrDefaultAsync(ct);

        var (accessToken, expiresAt) = GenerateJwtToken(user.Id, user.TenantId, user.Role.ToString(), vehicleId);
        var refreshToken = GenerateRefreshToken();

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Email = user.Email,
                BadgeNumber = user.BadgeNumber,
                Role = user.Role,
                TenantId = user.TenantId,
                IsActive = user.IsActive
            }
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst("userId")?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var user = await db.Users.FindAsync([userId], ct);
        if (user is null || user.IsDeleted) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return Problem(title: "Mot de passe actuel incorrect", statusCode: 400);

        if (req.NewPassword.Length < 8)
            return Problem(title: "Le nouveau mot de passe doit faire au moins 8 caractères", statusCode: 400);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Refresh([FromBody] RefreshTokenRequest request)
    {
        // In production, validate refresh token against a store.
        // Here we return Unauthorized as a safe stub (Infrastructure not yet wired).
        return Problem(title: "Refresh token invalide ou expiré", statusCode: 401);
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(Guid userId, Guid tenantId, string role, Guid? vehicleId = null)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var issuer = jwtSettings["Issuer"]!;
        var audience = jwtSettings["Audience"]!;
        var expirationHours = jwtSettings.GetValue<int>("ExpirationHours", 8);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(expirationHours);

        var claimsList = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("userId", userId.ToString()),
            new("tenantId", tenantId.ToString()),
            new(ClaimTypes.Role, role)
        };
        if (vehicleId.HasValue)
            claimsList.Add(new Claim("vehicleId", vehicleId.Value.ToString()));

        var claims = claimsList.ToArray();

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
