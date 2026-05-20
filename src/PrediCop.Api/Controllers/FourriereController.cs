using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/fourriere")]
[Authorize(Roles = "Admin,Manager,Operator")]
public class FourriereController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    private async Task<bool> IsModuleEnabledAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([TenantId], ct);
        return tenant?.ModuleFourriereEnabled ?? false;
    }

    /// <summary>
    /// GET /api/fourriere?status=&amp;agentId=&amp;dateFrom=&amp;dateTo=
    /// Liste paginée des véhicules en fourrière, triée par ImpoundedAt desc.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ImpoundedVehicleResponse>>> GetAll(
        [FromQuery] ImpoundStatus? status = null,
        [FromQuery] Guid? agentId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? plate = null,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<ImpoundedVehicle>()
            .Include(v => v.Agent)
            .Where(v => v.TenantId == TenantId);

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);

        if (agentId.HasValue)
            query = query.Where(v => v.AgentId == agentId.Value);

        if (dateFrom.HasValue)
            query = query.Where(v => v.ImpoundedAt >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(v => v.ImpoundedAt <= dateTo.Value.ToUniversalTime());

        if (!string.IsNullOrWhiteSpace(plate))
            query = query.Where(v => v.PlateNumber.Contains(plate));

        var vehicles = await query
            .OrderByDescending(v => v.ImpoundedAt)
            .ToListAsync(ct);

        return Ok(vehicles.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// GET /api/fourriere/{id}
    /// Détail d'un véhicule mis en fourrière.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImpoundedVehicleResponse>> GetById(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var vehicle = await db.Set<ImpoundedVehicle>()
            .Include(v => v.Agent)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        return Ok(MapToResponse(vehicle));
    }

    /// <summary>
    /// POST /api/fourriere
    /// Crée un nouvel enlèvement.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ImpoundedVehicleResponse>> Create(
        [FromBody] CreateImpoundRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        // Vérifier que l'agent appartient au tenant
        var agentExists = await db.Users
            .AnyAsync(u => u.Id == request.AgentId && u.TenantId == TenantId, ct);

        if (!agentExists)
            return Problem(title: "Agent introuvable ou n'appartient pas à ce tenant", statusCode: 400);

        var vehicle = new ImpoundedVehicle
        {
            TenantId = TenantId,
            PlateNumber = request.PlateNumber,
            Make = request.Make,
            Model = request.Model,
            Color = request.Color,
            Category = request.Category,
            Reason = request.Reason,
            AgentId = request.AgentId,
            OriginalAddress = request.OriginalAddress,
            StorageLocation = request.StorageLocation,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ConditionNotes = request.ConditionNotes,
            Notes = request.Notes,
            Status = ImpoundStatus.InStorage
        };

        db.Set<ImpoundedVehicle>().Add(vehicle);
        await db.SaveChangesAsync(ct);

        await db.Entry(vehicle).Reference(v => v.Agent).LoadAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, MapToResponse(vehicle));
    }

    /// <summary>
    /// PUT /api/fourriere/{id}
    /// Modifie les informations éditables d'un véhicule en fourrière.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ImpoundedVehicleResponse>> Update(
        Guid id,
        [FromBody] UpdateImpoundRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var vehicle = await db.Set<ImpoundedVehicle>()
            .Include(v => v.Agent)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        if (request.ConditionNotes is not null) vehicle.ConditionNotes = request.ConditionNotes;
        if (request.StorageLocation is not null) vehicle.StorageLocation = request.StorageLocation;
        if (request.Notes is not null) vehicle.Notes = request.Notes;
        if (request.PhotoUrls is not null) vehicle.PhotoUrls = request.PhotoUrls;

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(vehicle));
    }

    /// <summary>
    /// POST /api/fourriere/{id}/release
    /// Restitue le véhicule à son propriétaire.
    /// </summary>
    [HttpPost("{id:guid}/release")]
    public async Task<ActionResult<ImpoundedVehicleResponse>> Release(
        Guid id,
        [FromBody] ReleaseVehicleRequest request,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var vehicle = await db.Set<ImpoundedVehicle>()
            .Include(v => v.Agent)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        if (vehicle.Status != ImpoundStatus.InStorage)
            return Problem(title: "Le véhicule n'est plus en fourrière", statusCode: 400);

        vehicle.Status = ImpoundStatus.Released;
        vehicle.ReleasedAt = DateTime.UtcNow;
        vehicle.ReleasedToName = request.ReleasedToName;
        vehicle.ReleasedToIdNumber = request.ReleasedToIdNumber;
        if (request.Notes is not null) vehicle.Notes = request.Notes;

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(vehicle));
    }

    /// <summary>
    /// POST /api/fourriere/{id}/destroy
    /// Passe le véhicule au statut Destroyed.
    /// </summary>
    [HttpPost("{id:guid}/destroy")]
    public async Task<ActionResult<ImpoundedVehicleResponse>> Destroy(
        Guid id,
        CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var vehicle = await db.Set<ImpoundedVehicle>()
            .Include(v => v.Agent)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        if (vehicle.Status != ImpoundStatus.InStorage)
            return Problem(title: "Le véhicule n'est plus en fourrière", statusCode: 400);

        vehicle.Status = ImpoundStatus.Destroyed;
        vehicle.DestroyedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(vehicle));
    }

    /// <summary>
    /// GET /api/fourriere/stats
    /// Statistiques globales de la fourrière pour le tenant.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<FourriereStatsResponse>> GetStats(CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var statusGroups = await db.Set<ImpoundedVehicle>()
            .Where(v => v.TenantId == TenantId)
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var reasonGroups = await db.Set<ImpoundedVehicle>()
            .Where(v => v.TenantId == TenantId)
            .GroupBy(v => v.Reason)
            .Select(g => new { Reason = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalInStorage = statusGroups.FirstOrDefault(g => g.Status == ImpoundStatus.InStorage)?.Count ?? 0;
        var totalReleased = statusGroups.FirstOrDefault(g => g.Status == ImpoundStatus.Released)?.Count ?? 0;
        var totalDestroyed = statusGroups.FirstOrDefault(g => g.Status == ImpoundStatus.Destroyed)?.Count ?? 0;

        var byStatus = statusGroups.ToDictionary(g => g.Status.ToString(), g => g.Count);
        var byReason = reasonGroups.ToDictionary(g => g.Reason.ToString(), g => g.Count);

        return Ok(new FourriereStatsResponse(
            totalInStorage,
            totalReleased,
            totalDestroyed,
            byReason,
            byStatus
        ));
    }

    /// <summary>
    /// DELETE /api/fourriere/{id}
    /// Soft-delete d'un enregistrement de fourrière.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var vehicle = await db.Set<ImpoundedVehicle>()
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        vehicle.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // -------- Mapper --------

    private static ImpoundedVehicleResponse MapToResponse(ImpoundedVehicle v) => new(
        v.Id,
        v.PlateNumber,
        v.Make,
        v.Model,
        v.Color,
        v.Category,
        v.Reason,
        v.ImpoundedAt,
        v.AgentId,
        v.Agent?.FullName ?? string.Empty,
        v.Agent?.BadgeNumber ?? string.Empty,
        v.OriginalAddress,
        v.StorageLocation,
        v.Latitude,
        v.Longitude,
        v.ConditionNotes,
        v.PhotoUrls,
        v.Status,
        v.ReleasedAt,
        v.ReleasedToName,
        v.ReleasedToIdNumber,
        v.DestroyedAt,
        v.Notes,
        v.CreatedAt
    );
}

/// <summary>
/// DTO pour la modification partielle d'un enregistrement de fourrière.
/// </summary>
public record UpdateImpoundRequest(
    string? ConditionNotes,
    string? StorageLocation,
    string? Notes,
    string? PhotoUrls
);
