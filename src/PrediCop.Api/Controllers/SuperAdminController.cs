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
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController(AppDbContext db) : ControllerBase
{
    [HttpGet("tenants")]
    public async Task<ActionResult<List<TenantAdminDto>>> GetTenants(CancellationToken ct)
    {
        var tenants = await db.Tenants
            .OrderBy(t => t.Name)
            .Select(t => new TenantAdminDto
            {
                Id = t.Id,
                Name = t.Name,
                Slug = t.Slug,
                IsActive = t.IsActive,
                SubscriptionStatus = t.SubscriptionStatus.ToString(),
                SubscriptionPlan = t.SubscriptionPlan.ToString(),
                VehicleLimit = t.VehicleLimit,
                UserLimit = t.UserLimit,
                UserCount = t.Users.Count(u => !u.IsDeleted),
                VehicleCount = t.Vehicles.Count(v => !v.IsDeleted),
                CreatedAt = t.CreatedAt,
                TrialEndsAt = t.TrialEndsAt,
                CurrentPeriodEnd = t.CurrentPeriodEnd,
                StripeCustomerId = t.StripeCustomerId
            })
            .ToListAsync(ct);

        return Ok(tenants);
    }

    [HttpGet("tenants/{id:guid}")]
    public async Task<ActionResult<TenantAdminDto>> GetTenant(Guid id, CancellationToken ct)
    {
        var t = await db.Tenants
            .Where(x => x.Id == id)
            .Select(x => new TenantAdminDto
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                IsActive = x.IsActive,
                SubscriptionStatus = x.SubscriptionStatus.ToString(),
                SubscriptionPlan = x.SubscriptionPlan.ToString(),
                VehicleLimit = x.VehicleLimit,
                UserLimit = x.UserLimit,
                UserCount = x.Users.Count(u => !u.IsDeleted),
                VehicleCount = x.Vehicles.Count(v => !v.IsDeleted),
                CreatedAt = x.CreatedAt,
                TrialEndsAt = x.TrialEndsAt,
                CurrentPeriodEnd = x.CurrentPeriodEnd,
                StripeCustomerId = x.StripeCustomerId
            })
            .FirstOrDefaultAsync(ct);

        if (t is null) return NotFound();
        return Ok(t);
    }

    [HttpPost("tenants")]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            return Problem(title: "Ce slug est déjà utilisé.", statusCode: 409);

        if (!Enum.TryParse<SubscriptionPlan>(request.SubscriptionPlan, out var plan))
            plan = SubscriptionPlan.Essential;
        if (!Enum.TryParse<SubscriptionStatus>(request.SubscriptionStatus, out var status))
            status = SubscriptionStatus.Active;

        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            IsActive = true,
            SubscriptionPlan = plan,
            SubscriptionStatus = status,
            VehicleLimit = request.VehicleLimit,
            UserLimit = request.UserLimit
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        db.Users.Add(new User
        {
            TenantId = tenant.Id,
            FirstName = request.AdminFirstName.Trim(),
            LastName = request.AdminLastName.Trim(),
            Email = request.AdminEmail.Trim().ToLowerInvariant(),
            BadgeNumber = "ADMIN-001",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            Role = UserRole.Admin,
            IsActive = true
        });
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTenant), new { id = tenant.Id }, null);
    }

    [HttpPut("tenants/{id:guid}")]
    public async Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null) return NotFound();

        if (!Enum.TryParse<SubscriptionPlan>(request.SubscriptionPlan, out var plan))
            plan = tenant.SubscriptionPlan;
        if (!Enum.TryParse<SubscriptionStatus>(request.SubscriptionStatus, out var status))
            status = tenant.SubscriptionStatus;

        tenant.Name = request.Name.Trim();
        tenant.IsActive = request.IsActive;
        tenant.SubscriptionPlan = plan;
        tenant.SubscriptionStatus = status;
        tenant.VehicleLimit = request.VehicleLimit;
        tenant.UserLimit = request.UserLimit;
        tenant.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("tenants/{id:guid}/toggle-active")]
    public async Task<IActionResult> ToggleActive(Guid id, CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null) return NotFound();

        tenant.IsActive = !tenant.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
