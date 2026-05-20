using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/fleet")]
[Authorize(Roles = "Admin,Manager")]
public class FleetController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => (Guid)HttpContext.Items["TenantId"]!;

    private async Task<bool> IsModuleEnabledAsync(CancellationToken ct)
    {
        var tenant = await db.Tenants.FindAsync([TenantId], ct);
        return tenant?.ModuleFleetEnabled ?? false;
    }

    // ---- Carnet de bord ----

    [HttpGet("log-entries")]
    public async Task<ActionResult<List<VehicleLogEntryResponse>>> GetLogEntries(
        [FromQuery] Guid? vehicleId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<VehicleLogEntry>()
            .Include(e => e.Vehicle)
            .Include(e => e.Officer)
            .Where(e => e.TenantId == TenantId);

        if (vehicleId.HasValue)
            query = query.Where(e => e.VehicleId == vehicleId.Value);

        if (dateFrom.HasValue)
            query = query.Where(e => e.Date >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(e => e.Date <= dateTo.Value);

        var entries = await query
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        return Ok(entries.Select(MapLogEntryToResponse).ToList());
    }

    [HttpPost("log-entries")]
    public async Task<ActionResult<VehicleLogEntryResponse>> CreateLogEntry(
        [FromBody] CreateLogEntryRequest request,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var vehicleExists = await db.PatrolVehicles
            .AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);

        if (!vehicleExists)
            return Problem(title: "Véhicule non trouvé ou n'appartient pas à ce tenant", statusCode: 404);

        var entry = new VehicleLogEntry
        {
            TenantId = TenantId,
            VehicleId = request.VehicleId,
            OfficerId = request.OfficerId,
            Date = request.Date,
            KmStart = request.KmStart,
            KmEnd = request.KmEnd,
            FuelAdded = request.FuelAdded,
            Destination = request.Destination,
            Notes = request.Notes
        };

        db.Set<VehicleLogEntry>().Add(entry);
        await db.SaveChangesAsync(ct);

        await db.Entry(entry).Reference(e => e.Vehicle).LoadAsync(ct);
        await db.Entry(entry).Reference(e => e.Officer).LoadAsync(ct);

        return CreatedAtAction(nameof(GetLogEntries), new { vehicleId = entry.VehicleId }, MapLogEntryToResponse(entry));
    }

    // ---- Maintenances ----

    [HttpGet("maintenance")]
    public async Task<ActionResult<List<VehicleMaintenanceResponse>>> GetMaintenances(
        [FromQuery] Guid? vehicleId,
        [FromQuery] bool? upcoming,
        [FromQuery] bool? overdue,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var query = db.Set<VehicleMaintenance>()
            .Include(m => m.Vehicle)
            .Where(m => m.TenantId == TenantId);

        if (vehicleId.HasValue)
            query = query.Where(m => m.VehicleId == vehicleId.Value);

        var list = await query
            .OrderBy(m => m.ScheduledDate)
            .ToListAsync(ct);

        // Les filtres upcoming/overdue utilisent les propriétés calculées (post-materialisation)
        if (upcoming == true)
            list = list.Where(m => m.IsUpcoming || m.IsOverdue).ToList();
        else if (overdue == true)
            list = list.Where(m => m.IsOverdue).ToList();

        return Ok(list.Select(MapMaintenanceToResponse).ToList());
    }

    [HttpPost("maintenance")]
    public async Task<ActionResult<VehicleMaintenanceResponse>> CreateMaintenance(
        [FromBody] CreateMaintenanceRequest request,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var vehicleExists = await db.PatrolVehicles
            .AnyAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);

        if (!vehicleExists)
            return Problem(title: "Véhicule non trouvé ou n'appartient pas à ce tenant", statusCode: 404);

        var maintenance = new VehicleMaintenance
        {
            TenantId = TenantId,
            VehicleId = request.VehicleId,
            Type = request.Type,
            ScheduledDate = request.ScheduledDate,
            Description = request.Description,
            KmAtService = request.KmAtService,
            Cost = request.Cost,
            ProviderName = request.ProviderName,
            Notes = request.Notes
        };

        db.Set<VehicleMaintenance>().Add(maintenance);
        await db.SaveChangesAsync(ct);

        await db.Entry(maintenance).Reference(m => m.Vehicle).LoadAsync(ct);

        return CreatedAtAction(nameof(GetMaintenances), new { vehicleId = maintenance.VehicleId }, MapMaintenanceToResponse(maintenance));
    }

    [HttpPut("maintenance/{id:guid}")]
    public async Task<ActionResult<VehicleMaintenanceResponse>> UpdateMaintenance(
        Guid id,
        [FromBody] CreateMaintenanceRequest request,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var maintenance = await db.Set<VehicleMaintenance>()
            .Include(m => m.Vehicle)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (maintenance is null)
            return Problem(title: "Maintenance non trouvée", statusCode: 404);

        maintenance.Type = request.Type;
        maintenance.ScheduledDate = request.ScheduledDate;
        maintenance.Description = request.Description;
        if (request.KmAtService.HasValue) maintenance.KmAtService = request.KmAtService;
        if (request.Cost.HasValue) maintenance.Cost = request.Cost;
        if (request.ProviderName is not null) maintenance.ProviderName = request.ProviderName;
        if (request.Notes is not null) maintenance.Notes = request.Notes;

        await db.SaveChangesAsync(ct);
        return Ok(MapMaintenanceToResponse(maintenance));
    }

    [HttpPost("maintenance/{id:guid}/complete")]
    public async Task<ActionResult<VehicleMaintenanceResponse>> CompleteMaintenance(
        Guid id,
        [FromBody] CompleteMaintenanceRequest request,
        CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var maintenance = await db.Set<VehicleMaintenance>()
            .Include(m => m.Vehicle)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (maintenance is null)
            return Problem(title: "Maintenance non trouvée", statusCode: 404);

        maintenance.IsCompleted = true;
        maintenance.CompletedAt = request.CompletedAt;
        if (request.KmAtService.HasValue) maintenance.KmAtService = request.KmAtService;
        if (request.Cost.HasValue) maintenance.Cost = request.Cost;
        if (request.ProviderName is not null) maintenance.ProviderName = request.ProviderName;
        if (request.Notes is not null) maintenance.Notes = request.Notes;

        await db.SaveChangesAsync(ct);
        return Ok(MapMaintenanceToResponse(maintenance));
    }

    [HttpDelete("maintenance/{id:guid}")]
    public async Task<IActionResult> DeleteMaintenance(Guid id, CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var maintenance = await db.Set<VehicleMaintenance>()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (maintenance is null)
            return Problem(title: "Maintenance non trouvée", statusCode: 404);

        maintenance.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---- Alertes ----

    [HttpGet("alerts")]
    public async Task<ActionResult<List<FleetAlertResponse>>> GetAlerts(CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var now = DateTime.UtcNow;
        var in30Days = now.AddDays(30);

        var maintenances = await db.Set<VehicleMaintenance>()
            .Include(m => m.Vehicle)
            .Where(m => m.TenantId == TenantId && !m.IsCompleted && m.ScheduledDate < in30Days)
            .OrderBy(m => m.ScheduledDate)
            .ToListAsync(ct);

        var alerts = maintenances.Select(m => new FleetAlertResponse(
            VehicleId: m.VehicleId,
            VehicleCallSign: m.Vehicle.CallSign,
            VehiclePlate: m.Vehicle.LicensePlate,
            AlertType: m.Type.ToString(),
            Description: m.Description,
            DueDate: m.ScheduledDate
        )).ToList();

        return Ok(alerts);
    }

    // ---- Résumé flotte ----

    [HttpGet("summary")]
    public async Task<ActionResult<List<VehicleSummaryResponse>>> GetSummary(CancellationToken ct = default)
    {
        if (!await IsModuleEnabledAsync(ct))
            return Problem(title: "Module non activé pour ce tenant", statusCode: 403);

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonth = DateOnly.FromDateTime(now);
        var firstOfMonth = new DateOnly(currentMonth.Year, currentMonth.Month, 1);

        var vehicles = await db.PatrolVehicles
            .Where(v => v.TenantId == TenantId)
            .OrderBy(v => v.CallSign)
            .ToListAsync(ct);

        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        // Entrées du carnet de bord du mois en cours
        var logEntries = await db.Set<VehicleLogEntry>()
            .Where(e => e.TenantId == TenantId
                && vehicleIds.Contains(e.VehicleId)
                && e.Date >= firstOfMonth)
            .ToListAsync(ct);

        // Maintenances non complétées
        var maintenances = await db.Set<VehicleMaintenance>()
            .Where(m => m.TenantId == TenantId
                && vehicleIds.Contains(m.VehicleId)
                && !m.IsCompleted)
            .OrderBy(m => m.ScheduledDate)
            .ToListAsync(ct);

        var summaries = vehicles.Select(v =>
        {
            var vehicleLogs = logEntries.Where(e => e.VehicleId == v.Id).ToList();
            var totalKm = vehicleLogs.Sum(e => e.KmTotal);
            var totalTrips = vehicleLogs.Count;

            var nextMaintenance = maintenances
                .Where(m => m.VehicleId == v.Id)
                .OrderBy(m => m.ScheduledDate)
                .FirstOrDefault();

            var hasOverdue = maintenances
                .Any(m => m.VehicleId == v.Id && m.IsOverdue);

            return new VehicleSummaryResponse(
                Id: v.Id,
                CallSign: v.CallSign,
                LicensePlate: v.LicensePlate,
                TotalKmThisMonth: totalKm,
                TotalTrips: totalTrips,
                NextMaintenanceDate: nextMaintenance?.ScheduledDate,
                NextMaintenanceType: nextMaintenance?.Type.ToString(),
                HasOverdueMaintenance: hasOverdue
            );
        }).ToList();

        return Ok(summaries);
    }

    // ---- Mappers ----

    private static VehicleLogEntryResponse MapLogEntryToResponse(VehicleLogEntry e) => new(
        Id: e.Id,
        VehicleId: e.VehicleId,
        VehicleCallSign: e.Vehicle?.CallSign ?? string.Empty,
        VehiclePlate: e.Vehicle?.LicensePlate ?? string.Empty,
        OfficerId: e.OfficerId,
        OfficerFullName: e.Officer?.FullName ?? string.Empty,
        Date: e.Date,
        KmStart: e.KmStart,
        KmEnd: e.KmEnd,
        KmTotal: e.KmTotal,
        FuelAdded: e.FuelAdded,
        Destination: e.Destination,
        Notes: e.Notes,
        CreatedAt: e.CreatedAt
    );

    private static VehicleMaintenanceResponse MapMaintenanceToResponse(VehicleMaintenance m) => new(
        Id: m.Id,
        VehicleId: m.VehicleId,
        VehicleCallSign: m.Vehicle?.CallSign ?? string.Empty,
        VehiclePlate: m.Vehicle?.LicensePlate ?? string.Empty,
        Type: m.Type,
        ScheduledDate: m.ScheduledDate,
        CompletedAt: m.CompletedAt,
        KmAtService: m.KmAtService,
        Description: m.Description,
        Cost: m.Cost,
        ProviderName: m.ProviderName,
        Notes: m.Notes,
        IsCompleted: m.IsCompleted,
        IsOverdue: m.IsOverdue,
        IsUpcoming: m.IsUpcoming
    );
}
