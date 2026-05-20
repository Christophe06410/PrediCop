using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/tenant/features")]
public class TenantFeaturesController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    /// <summary>
    /// GET /api/tenant/features
    /// Retourne les feature flags du tenant courant.
    /// AllowAnonymous : si non authentifié (ou tenant inconnu), retourne les flags par défaut.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<TenantFeatureFlagsResponse>> GetFeatures(CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("tenantId")?.Value;
        if (!Guid.TryParse(tenantIdClaim, out var tid))
            return Ok(GetDefaultFlags());

        var tenant = await db.Tenants.FindAsync([tid], ct);
        if (tenant is null)
            return Ok(GetDefaultFlags());

        return Ok(MapToResponse(tenant));
    }

    /// <summary>
    /// PATCH /api/tenant/features/modules
    /// Met à jour les flags de modules optionnels (Admin uniquement).
    /// </summary>
    [HttpPatch("modules")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TenantFeatureFlagsResponse>> UpdateModuleFlags(
        [FromBody] UpdateModuleFlagsRequest request,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([TenantId], ct);
        if (tenant is null)
            return Problem(title: "Tenant non trouvé", statusCode: 404);

        tenant.ModuleRhEnabled = request.ModuleRhEnabled;
        tenant.ModuleFourriereEnabled = request.ModuleFourriereEnabled;
        tenant.ModuleFleetEnabled = request.ModuleFleetEnabled;
        tenant.ModuleLogisticsEnabled = request.ModuleLogisticsEnabled;
        tenant.ModuleVerbalisationEnabled = request.ModuleVerbalisationEnabled;
        tenant.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(MapToResponse(tenant));
    }

    /// <summary>
    /// PATCH /api/tenant/features/sensitive-fields
    /// Met à jour les flags de champs sensibles et rétention RGPD (Admin uniquement).
    /// </summary>
    [HttpPatch("sensitive-fields")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TenantFeatureFlagsResponse>> UpdateSensitiveFieldFlags(
        [FromBody] UpdateSensitiveFieldFlagsRequest request,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([TenantId], ct);
        if (tenant is null)
            return Problem(title: "Tenant non trouvé", statusCode: 404);

        tenant.AgentBloodTypeEnabled = request.AgentBloodTypeEnabled;
        tenant.AgentEmergencyContactEnabled = request.AgentEmergencyContactEnabled;
        tenant.GpsTrackingEnabled = request.GpsTrackingEnabled;
        tenant.GeofencingEnabled = request.GeofencingEnabled;
        tenant.PhotoAttachmentsEnabled = request.PhotoAttachmentsEnabled;
        tenant.GpsDataRetentionDays = request.GpsDataRetentionDays;
        tenant.AuditLogRetentionDays = request.AuditLogRetentionDays;
        tenant.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(MapToResponse(tenant));
    }

    // ---- Helpers ----

    private static TenantFeatureFlagsResponse GetDefaultFlags() => new(
        ModuleRhEnabled: false,
        ModuleFourriereEnabled: false,
        ModuleFleetEnabled: false,
        ModuleLogisticsEnabled: false,
        ModuleVerbalisationEnabled: false,
        AgentBloodTypeEnabled: false,
        AgentEmergencyContactEnabled: true,
        GpsTrackingEnabled: true,
        GeofencingEnabled: false,
        PhotoAttachmentsEnabled: true,
        GpsDataRetentionDays: 30,
        AuditLogRetentionDays: 365
    );

    private static TenantFeatureFlagsResponse MapToResponse(Tenant t) => new(
        ModuleRhEnabled: t.ModuleRhEnabled,
        ModuleFourriereEnabled: t.ModuleFourriereEnabled,
        ModuleFleetEnabled: t.ModuleFleetEnabled,
        ModuleLogisticsEnabled: t.ModuleLogisticsEnabled,
        ModuleVerbalisationEnabled: t.ModuleVerbalisationEnabled,
        AgentBloodTypeEnabled: t.AgentBloodTypeEnabled,
        AgentEmergencyContactEnabled: t.AgentEmergencyContactEnabled,
        GpsTrackingEnabled: t.GpsTrackingEnabled,
        GeofencingEnabled: t.GeofencingEnabled,
        PhotoAttachmentsEnabled: t.PhotoAttachmentsEnabled,
        GpsDataRetentionDays: t.GpsDataRetentionDays,
        AuditLogRetentionDays: t.AuditLogRetentionDays
    );
}
