using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/logistics")]
[Authorize(Roles = "Admin,Manager")]
public class LogisticsController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    private async Task<bool> IsModuleEnabledAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([TenantId], ct);
        return tenant?.ModuleLogisticsEnabled ?? false;
    }

    // -------- Catalogue --------

    [HttpGet("catalog")]
    public async Task<ActionResult<List<EquipmentCatalogResponse>>> GetCatalog(
        [FromQuery] EquipmentCategory? category,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<EquipmentCatalog>()
            .Where(e => e.TenantId == TenantId);

        if (activeOnly)
            query = query.Where(e => e.IsActive);

        if (category.HasValue)
            query = query.Where(e => e.Category == category.Value);

        var items = await query
            .OrderBy(e => e.Category)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);

        return Ok(items.Select(MapCatalogToResponse).ToList());
    }

    [HttpPost("catalog")]
    public async Task<ActionResult<EquipmentCatalogResponse>> CreateEquipment(
        [FromBody] CreateEquipmentRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var item = new EquipmentCatalog
        {
            TenantId = TenantId,
            Name = request.Name,
            Category = request.Category,
            Description = request.Description,
            Unit = request.Unit,
            DefaultLifespanMonths = request.DefaultLifespanMonths,
            ReferenceCode = request.ReferenceCode,
            IsActive = true
        };

        db.Set<EquipmentCatalog>().Add(item);
        await db.SaveChangesAsync(ct);

        return Ok(MapCatalogToResponse(item));
    }

    [HttpPut("catalog/{id:guid}")]
    public async Task<ActionResult<EquipmentCatalogResponse>> UpdateEquipment(
        Guid id,
        [FromBody] UpdateEquipmentRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var item = await db.Set<EquipmentCatalog>()
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId, ct);

        if (item is null)
            return Problem(title: "Équipement non trouvé", statusCode: 404);

        item.Name = request.Name;
        item.Category = request.Category;
        item.Description = request.Description;
        item.Unit = request.Unit;
        item.DefaultLifespanMonths = request.DefaultLifespanMonths;
        item.ReferenceCode = request.ReferenceCode;
        item.IsActive = request.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(MapCatalogToResponse(item));
    }

    [HttpDelete("catalog/{id:guid}")]
    public async Task<IActionResult> DeleteEquipment(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var item = await db.Set<EquipmentCatalog>()
            .FirstOrDefaultAsync(e => e.Id == id && e.TenantId == TenantId, ct);

        if (item is null)
            return Problem(title: "Équipement non trouvé", statusCode: 404);

        item.IsDeleted = true;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -------- Dotations --------

    [HttpGet("issuances")]
    public async Task<ActionResult<List<EquipmentIssuanceResponse>>> GetIssuances(
        [FromQuery] Guid? agentId,
        [FromQuery] Guid? equipmentId,
        [FromQuery] bool? expiredOnly,
        [FromQuery] bool? notReturned,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<EquipmentIssuance>()
            .Include(i => i.Agent)
            .Include(i => i.Equipment)
            .Where(i => i.TenantId == TenantId);

        if (agentId.HasValue)
            query = query.Where(i => i.AgentId == agentId.Value);

        if (equipmentId.HasValue)
            query = query.Where(i => i.EquipmentCatalogId == equipmentId.Value);

        if (notReturned == true)
            query = query.Where(i => !i.IsReturned);

        var items = await query
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync(ct);

        if (expiredOnly == true)
            items = items.Where(i => i.IsExpired).ToList();

        return Ok(items.Select(MapIssuanceToResponse).ToList());
    }

    [HttpPost("issuances")]
    public async Task<ActionResult<EquipmentIssuanceResponse>> CreateIssuance(
        [FromBody] CreateIssuanceRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentExists = await db.Users.AnyAsync(u => u.Id == request.AgentId && u.TenantId == TenantId, ct);
        if (!agentExists)
            return Problem(title: "Agent non trouvé", statusCode: 404);

        var equipment = await db.Set<EquipmentCatalog>()
            .FirstOrDefaultAsync(e => e.Id == request.EquipmentCatalogId && e.TenantId == TenantId, ct);

        if (equipment is null)
            return Problem(title: "Équipement non trouvé dans le catalogue", statusCode: 404);

        var issuedAt = DateTime.UtcNow;
        DateTime? expiresAt = request.ExpiresAt;

        if (!expiresAt.HasValue && equipment.DefaultLifespanMonths.HasValue)
            expiresAt = issuedAt.AddMonths(equipment.DefaultLifespanMonths.Value);

        var issuance = new EquipmentIssuance
        {
            TenantId = TenantId,
            AgentId = request.AgentId,
            EquipmentCatalogId = request.EquipmentCatalogId,
            Quantity = request.Quantity,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Size = request.Size,
            SerialNumber = request.SerialNumber,
            Notes = request.Notes,
            IsReturned = false
        };

        db.Set<EquipmentIssuance>().Add(issuance);
        await db.SaveChangesAsync(ct);

        await db.Entry(issuance).Reference(i => i.Agent).LoadAsync(ct);
        await db.Entry(issuance).Reference(i => i.Equipment).LoadAsync(ct);

        return Ok(MapIssuanceToResponse(issuance));
    }

    [HttpPost("issuances/{id:guid}/return")]
    public async Task<ActionResult<EquipmentIssuanceResponse>> ReturnIssuance(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var issuance = await db.Set<EquipmentIssuance>()
            .Include(i => i.Agent)
            .Include(i => i.Equipment)
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId, ct);

        if (issuance is null)
            return Problem(title: "Dotation non trouvée", statusCode: 404);

        issuance.IsReturned = true;
        issuance.ReturnedAt = DateTime.UtcNow;
        issuance.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(MapIssuanceToResponse(issuance));
    }

    [HttpDelete("issuances/{id:guid}")]
    public async Task<IActionResult> DeleteIssuance(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var issuance = await db.Set<EquipmentIssuance>()
            .FirstOrDefaultAsync(i => i.Id == id && i.TenantId == TenantId, ct);

        if (issuance is null)
            return Problem(title: "Dotation non trouvée", statusCode: 404);

        issuance.IsDeleted = true;
        issuance.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // -------- Profils uniformes --------

    [HttpGet("uniforms/{agentId:guid}")]
    public async Task<ActionResult<UniformProfileResponse>> GetUniformProfile(Guid agentId, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var profile = await db.Set<UniformProfile>()
            .Include(p => p.Agent)
            .FirstOrDefaultAsync(p => p.AgentId == agentId && p.TenantId == TenantId, ct);

        if (profile is null)
            return Problem(title: "Profil uniforme non trouvé", statusCode: 404);

        return Ok(MapUniformProfileToResponse(profile));
    }

    [HttpPost("uniforms/{agentId:guid}")]
    public async Task<ActionResult<UniformProfileResponse>> UpsertUniformProfile(
        Guid agentId,
        [FromBody] UpsertUniformProfileRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var agentExists = await db.Users.AnyAsync(u => u.Id == agentId && u.TenantId == TenantId, ct);
        if (!agentExists)
            return Problem(title: "Agent non trouvé", statusCode: 404);

        var profile = await db.Set<UniformProfile>()
            .Include(p => p.Agent)
            .FirstOrDefaultAsync(p => p.AgentId == agentId && p.TenantId == TenantId, ct);

        if (profile is null)
        {
            profile = new UniformProfile
            {
                TenantId = TenantId,
                AgentId = agentId
            };
            db.Set<UniformProfile>().Add(profile);
        }

        profile.JacketSize = request.JacketSize;
        profile.PantSize = request.PantSize;
        profile.ShirtSize = request.ShirtSize;
        profile.ShoeSize = request.ShoeSize;
        profile.HatSize = request.HatSize;
        profile.Notes = request.Notes;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        if (profile.Agent is null)
            await db.Entry(profile).Reference(p => p.Agent).LoadAsync(ct);

        return Ok(MapUniformProfileToResponse(profile));
    }

    // -------- Alertes --------

    [HttpGet("alerts")]
    public async Task<ActionResult<List<LogisticsAlertResponse>>> GetAlerts(CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var now = DateTime.UtcNow;
        var threshold = now.AddDays(60);

        var issuances = await db.Set<EquipmentIssuance>()
            .Include(i => i.Agent)
            .Include(i => i.Equipment)
            .Where(i => i.TenantId == TenantId
                && !i.IsReturned
                && i.ExpiresAt.HasValue
                && i.ExpiresAt <= threshold)
            .OrderBy(i => i.ExpiresAt)
            .ToListAsync(ct);

        var alerts = issuances.Select(i => new LogisticsAlertResponse(
            i.Id,
            i.AgentId,
            i.Agent?.FullName ?? string.Empty,
            i.Equipment?.Name ?? string.Empty,
            i.ExpiresAt!.Value,
            i.IsExpired
        )).ToList();

        return Ok(alerts);
    }

    // -------- Mappers --------

    private static EquipmentCatalogResponse MapCatalogToResponse(EquipmentCatalog e) => new(
        e.Id,
        e.Name,
        e.Category,
        e.Description,
        e.Unit,
        e.DefaultLifespanMonths,
        e.ReferenceCode,
        e.IsActive);

    private static EquipmentIssuanceResponse MapIssuanceToResponse(EquipmentIssuance i) => new(
        i.Id,
        i.AgentId,
        i.Agent?.FullName ?? string.Empty,
        i.Agent?.BadgeNumber ?? string.Empty,
        i.EquipmentCatalogId,
        i.Equipment?.Name ?? string.Empty,
        i.Equipment?.Category ?? EquipmentCategory.Autre,
        i.Quantity,
        i.Equipment?.Unit ?? "pièce",
        i.IssuedAt,
        i.ExpiresAt,
        i.Size,
        i.SerialNumber,
        i.Notes,
        i.IsReturned,
        i.ReturnedAt,
        i.IsExpired,
        i.ExpiresWithin60Days);

    private static UniformProfileResponse MapUniformProfileToResponse(UniformProfile p) => new(
        p.Id,
        p.AgentId,
        p.Agent?.FullName ?? string.Empty,
        p.Agent?.BadgeNumber ?? string.Empty,
        p.JacketSize,
        p.PantSize,
        p.ShirtSize,
        p.ShoeSize,
        p.HatSize,
        p.Notes);
}
