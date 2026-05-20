using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PrediCop.Core.DTOs;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, IConfiguration configuration, ITotpService totpService) : ControllerBase
{
    [HttpGet("tenants")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TenantSummaryDto>>> GetTenants(CancellationToken ct)
    {
        var tenants = await db.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new TenantSummaryDto { Id = t.Id, Name = t.Name, Slug = t.Slug })
            .ToListAsync(ct);
        return Ok(tenants);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TenantSlug))
            return Problem(title: "La ville est requise", statusCode: 400);

        var tenant = await db.Tenants
            .FirstOrDefaultAsync(t => t.Slug == request.TenantSlug && t.IsActive, ct);
        if (tenant is null)
            return Problem(title: "Ville introuvable", statusCode: 400);

        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u =>
                u.Email == request.Email &&
                u.TenantId == tenant.Id &&
                u.IsActive && !u.IsDeleted, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Problem(title: "Identifiants invalides", statusCode: 401);

        // Si l'utilisateur est Admin/Manager et a activé la 2FA, retourner un TempToken
        if (user.TotpEnabled && (user.Role == UserRole.Admin || user.Role == UserRole.Manager))
        {
            var tempToken = GenerateTempToken(user.Id);
            return Ok(new LoginResponse
            {
                RequiresTwoFactor = true,
                TempToken = tempToken
            });
        }

        // Resolve the vehicle this officer is currently assigned to (used for mission filtering on mobile)
        var vehicleId = await db.VehicleOfficers
            .Where(vo => vo.UserId == user.Id && vo.IsActive)
            .Select(vo => (Guid?)vo.VehicleId)
            .FirstOrDefaultAsync(ct);

        var (accessToken, expiresAt) = GenerateJwtToken(user.Id, user.TenantId, user.Role.ToString(), vehicleId, tenant);
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
                TenantName = user.Tenant?.Name ?? "",
                TenantSlug = user.Tenant?.Slug ?? "",
                IsActive = user.IsActive,
                VehicleId = vehicleId
            }
        });
    }

    // ─── 2FA Endpoints ───────────────────────────────────────────────────────

    [HttpGet("2fa/status")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<TotpStatusResponse>> GetTotpStatus(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return NotFound();

        return Ok(new TotpStatusResponse(user.TotpEnabled));
    }

    [HttpPost("2fa/setup")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<TotpSetupResponse>> Setup2Fa(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return NotFound();

        var secret = totpService.GenerateSecret();
        var qrCodeUri = totpService.GenerateQrCodeUri(user.Email, user.Tenant?.Name ?? "PrediCop", secret);
        var recoveryCodes = totpService.GenerateRecoveryCodes();

        // Sauvegarder le secret temporairement (TotpEnabled reste false jusqu'à la confirmation)
        user.TotpSecretKey = secret;
        user.TotpRecoveryCodes = JsonSerializer.Serialize(recoveryCodes);
        await db.SaveChangesAsync(ct);

        return Ok(new TotpSetupResponse(secret, qrCodeUri, recoveryCodes));
    }

    [HttpPost("2fa/enable")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Enable2Fa([FromBody] TotpEnableRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return NotFound();

        if (string.IsNullOrWhiteSpace(user.TotpSecretKey))
            return Problem(title: "Aucun secret TOTP configuré. Appelez /2fa/setup d'abord.", statusCode: 400);

        if (!totpService.VerifyCode(user.TotpSecretKey, request.Code))
            return Problem(title: "Code TOTP invalide.", statusCode: 400);

        user.TotpEnabled = true;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("2fa/disable")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Disable2Fa([FromBody] TotpEnableRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);
        if (user is null) return NotFound();

        if (user.TotpEnabled)
        {
            if (string.IsNullOrWhiteSpace(user.TotpSecretKey) || !totpService.VerifyCode(user.TotpSecretKey, request.Code))
                return Problem(title: "Code TOTP invalide.", statusCode: 400);
        }

        user.TotpEnabled = false;
        user.TotpSecretKey = null;
        user.TotpRecoveryCodes = null;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Verify2Fa([FromBody] TotpVerifyRequest request, CancellationToken ct)
    {
        // Valider le TempToken et extraire l'userId
        var userId = ValidateTempToken(request.TempToken);
        if (userId is null)
            return Problem(title: "Token temporaire invalide ou expiré.", statusCode: 401);

        var user = await db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted, ct);
        if (user is null)
            return Problem(title: "Utilisateur introuvable.", statusCode: 401);

        if (!user.TotpEnabled || string.IsNullOrWhiteSpace(user.TotpSecretKey))
            return Problem(title: "La 2FA n'est pas activée pour cet utilisateur.", statusCode: 400);

        // Vérifier le code TOTP ou un code de récupération
        bool codeValid = totpService.VerifyCode(user.TotpSecretKey, request.Code);

        if (!codeValid && !string.IsNullOrWhiteSpace(user.TotpRecoveryCodes))
        {
            var (recoveryValid, updatedJson) = totpService.VerifyRecoveryCode(user.TotpRecoveryCodes, request.Code);
            if (recoveryValid)
            {
                user.TotpRecoveryCodes = updatedJson;
                await db.SaveChangesAsync(ct);
                codeValid = true;
            }
        }

        if (!codeValid)
            return Problem(title: "Code invalide.", statusCode: 401);

        var vehicleId = await db.VehicleOfficers
            .Where(vo => vo.UserId == user.Id && vo.IsActive)
            .Select(vo => (Guid?)vo.VehicleId)
            .FirstOrDefaultAsync(ct);

        var (accessToken, expiresAt) = GenerateJwtToken(user.Id, user.TenantId, user.Role.ToString(), vehicleId, user.Tenant);
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
                TenantName = user.Tenant?.Name ?? "",
                TenantSlug = user.Tenant?.Slug ?? "",
                IsActive = user.IsActive,
                VehicleId = vehicleId
            }
        });
    }

    // ─── Other endpoints ─────────────────────────────────────────────────────

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

    [HttpPost("select-vehicle/{vehicleId:guid}")]
    [Authorize]
    public async Task<ActionResult<SelectVehicleResponse>> SelectVehicle(Guid vehicleId, CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("userId")!.Value);
        var tenantId = Guid.Parse(User.FindFirst("tenantId")!.Value);
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)!.Value;

        var vehicle = await db.PatrolVehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == tenantId, ct);
        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        // Deactivate any existing active assignment for this user
        var existing = await db.VehicleOfficers
            .Where(vo => vo.UserId == userId && vo.IsActive)
            .ToListAsync(ct);
        foreach (var vo in existing)
        {
            vo.IsActive = false;
            vo.UnassignedAt = DateTime.UtcNow;
        }

        // Reactivate or create the assignment for the chosen vehicle
        var assignment = await db.VehicleOfficers
            .FirstOrDefaultAsync(vo => vo.VehicleId == vehicleId && vo.UserId == userId, ct);
        if (assignment != null)
        {
            assignment.IsActive = true;
            assignment.AssignedAt = DateTime.UtcNow;
            assignment.UnassignedAt = null;
        }
        else
        {
            db.VehicleOfficers.Add(new Core.Entities.VehicleOfficer
            {
                VehicleId = vehicleId,
                UserId = userId,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(ct);

        var tenantForToken = await db.Tenants.FindAsync([tenantId], ct);
        var (accessToken, expiresAt) = GenerateJwtToken(userId, tenantId, role, vehicleId, tenantForToken);
        return Ok(new SelectVehicleResponse
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt,
            VehicleId = vehicleId,
            VehicleCallSign = vehicle.CallSign
        });
    }

    [HttpPost("device-token")]
    [Authorize]
    public async Task<IActionResult> RegisterDeviceToken([FromBody] DeviceTokenRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var user = await db.Users.FindAsync([userId.Value], ct);
        if (user is null || user.IsDeleted) return NotFound();

        user.DeviceToken = request.DeviceToken;
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

    // ─── Private helpers ─────────────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst("userId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private string GenerateTempToken(Guid userId)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var issuer = jwtSettings["Issuer"]!;
        var audience = jwtSettings["Audience"]!;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(5);

        var claims = new[]
        {
            new Claim("twoFactorUserId", userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Guid? ValidateTempToken(string tempToken)
    {
        if (string.IsNullOrWhiteSpace(tempToken))
            return null;

        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"]!;
        var issuer = jwtSettings["Issuer"]!;
        var audience = jwtSettings["Audience"]!;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(tempToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var claim = principal.FindFirst("twoFactorUserId")?.Value;
            return Guid.TryParse(claim, out var userId) ? userId : null;
        }
        catch
        {
            return null;
        }
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(
        Guid userId, Guid tenantId, string role, Guid? vehicleId = null,
        Core.Entities.Tenant? tenant = null)
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

        if (tenant is not null)
        {
            claimsList.Add(new Claim("subscriptionStatus", tenant.SubscriptionStatus.ToString()));
            claimsList.Add(new Claim("subscriptionPlan", tenant.SubscriptionPlan.ToString()));
            claimsList.Add(new Claim("vehicleLimit", tenant.VehicleLimit.ToString()));
            claimsList.Add(new Claim("userLimit", tenant.UserLimit.ToString()));
        }

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
public record TotpStatusResponse(bool TotpEnabled);
public record DeviceTokenRequest(string DeviceToken);
