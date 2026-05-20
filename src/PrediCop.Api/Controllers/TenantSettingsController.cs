using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantSettingsController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    /// <summary>Active ou désactive le géofencing pour le tenant courant.</summary>
    [HttpPatch("geofencing")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetGeofencing(
        [FromBody] SetGeofencingRequest request,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId, ct);
        if (tenant is null)
            return Problem(title: "Tenant non trouvé", statusCode: 404);

        tenant.GeofencingEnabled = request.Enabled;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Met à jour l'email DPO (RGPD) du tenant courant.</summary>
    [HttpPatch("dpo-email")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetDpoEmail(
        [FromBody] SetDpoEmailRequest request,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantId, ct);
        if (tenant is null)
            return Problem(title: "Tenant non trouvé", statusCode: 404);

        tenant.DpoEmail = request.DpoEmail;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Retourne les paramètres tenant courants (géofencing, DPO).</summary>
    [HttpGet("settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TenantSettingsResponse>> GetSettings(CancellationToken ct)
    {
        var tenant = await db.Tenants
            .Where(t => t.Id == TenantId)
            .Select(t => new TenantSettingsResponse
            {
                GeofencingEnabled = t.GeofencingEnabled,
                DpoEmail = t.DpoEmail
            })
            .FirstOrDefaultAsync(ct);

        if (tenant is null)
            return Problem(title: "Tenant non trouvé", statusCode: 404);

        return Ok(tenant);
    }
}
