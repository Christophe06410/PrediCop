using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;
using PrediCop.Api.Hubs;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehiclesController(
    AppDbContext db,
    IGpsService gpsService,
    IHubContext<PoliceHub> hubContext,
    IEmailService emailService) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    [HttpGet]
    public async Task<ActionResult<List<VehicleResponse>>> GetVehicles(CancellationToken ct)
    {
        var vehicles = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .Where(v => v.TenantId == TenantId)
            .OrderBy(v => v.CallSign)
            .ToListAsync(ct);

        return Ok(vehicles.Select(MapToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VehicleResponse>> GetVehicle(Guid id, CancellationToken ct)
    {
        var vehicle = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        return Ok(MapToResponse(vehicle));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<VehicleResponse>> CreateVehicle(
        [FromBody] CreateVehicleRequest request,
        CancellationToken ct)
    {
        if (!Enum.TryParse<VehicleStatus>(request.Status, out var status))
            status = VehicleStatus.Offline;

        var vehicle = new PatrolVehicle
        {
            TenantId = TenantId,
            CallSign = request.CallSign,
            LicensePlate = request.LicensePlate,
            Status = status,
            BeaconUuid = request.BeaconUuid,
            Capacity = request.Capacity
        };

        db.PatrolVehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        var created = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .FirstAsync(v => v.Id == vehicle.Id, ct);

        return CreatedAtAction(nameof(GetVehicle), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<VehicleResponse>> UpdateVehicle(
        Guid id,
        [FromBody] UpdateVehicleRequest request,
        CancellationToken ct)
    {
        var vehicle = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        if (request.CallSign is not null) vehicle.CallSign = request.CallSign;
        if (request.LicensePlate is not null) vehicle.LicensePlate = request.LicensePlate;
        if (request.BeaconUuid is not null) vehicle.BeaconUuid = request.BeaconUuid;
        if (request.Status is not null && Enum.TryParse<VehicleStatus>(request.Status, out var status))
            vehicle.Status = status;
        if (request.Capacity.HasValue) vehicle.Capacity = request.Capacity.Value;

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(vehicle));
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<VehicleResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateVehicleStatusRequest request,
        CancellationToken ct)
    {
        var vehicle = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        vehicle.Status = request.Status;
        await db.SaveChangesAsync(ct);

        return Ok(MapToResponse(vehicle));
    }

    [HttpPost("{id:guid}/position")]
    public async Task<ActionResult> UpdatePosition(
        Guid id,
        [FromBody] VehiclePositionUpdate request,
        CancellationToken ct)
    {
        var vehicle = await db.PatrolVehicles
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        await gpsService.UpdateVehiclePositionAsync(id, request.Latitude, request.Longitude, ct);

        var positionUpdate = new VehiclePositionUpdate
        {
            VehicleId = id,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            UpdatedAt = DateTime.UtcNow
        };

        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("VehiclePositionUpdated", positionUpdate, ct);

        return NoContent();
    }

    [HttpPut("{id:guid}/geozone")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> AssignGeoZone(
        Guid id,
        [FromBody] AssignGeoZoneRequest request,
        CancellationToken ct)
    {
        var vehicle = await db.PatrolVehicles
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        if (request.GeoZoneId.HasValue)
        {
            // Vérifier que la zone appartient au même tenant
            var zoneExists = await db.GeoZones
                .AnyAsync(z => z.Id == request.GeoZoneId.Value && z.TenantId == TenantId, ct);

            if (!zoneExists)
                return Problem(title: "Zone de patrouille non trouvée", statusCode: 404);
        }

        vehicle.AssignedGeoZoneId = request.GeoZoneId;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpGet("crew-sheet")]
    public async Task<ActionResult<List<CrewSheetEntryResponse>>> GetCrewSheet(CancellationToken ct)
    {
        var vehicles = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .Include(v => v.Missions.Where(m =>
                m.Mission.Status == MissionStatus.Accepted ||
                m.Mission.Status == MissionStatus.InProgress))
                .ThenInclude(a => a.Mission)
            .Where(v => v.TenantId == TenantId && v.Status != VehicleStatus.Offline)
            .OrderBy(v => v.CallSign)
            .ToListAsync(ct);

        var result = vehicles.Select(v =>
        {
            var activeAssignment = v.Missions
                .Where(a => a.Mission.Status == MissionStatus.Accepted ||
                            a.Mission.Status == MissionStatus.InProgress)
                .OrderByDescending(a => a.Mission.AcceptedAt)
                .FirstOrDefault();

            return new CrewSheetEntryResponse
            {
                VehicleId = v.Id,
                CallSign = v.CallSign,
                LicensePlate = v.LicensePlate,
                Status = v.Status.ToString(),
                LastLatitude = v.LastLatitude,
                LastLongitude = v.LastLongitude,
                Officers = v.Officers
                    .Where(o => o.IsActive)
                    .Select(o => new CrewMemberInfo
                    {
                        UserId = o.UserId,
                        FullName = o.User.FullName,
                        BadgeNumber = o.User.BadgeNumber
                    }).ToList(),
                CurrentMission = activeAssignment is null ? null : new ActiveMissionInfo
                {
                    MissionId = activeAssignment.MissionId,
                    Reference = activeAssignment.Mission.Reference,
                    Priority = activeAssignment.Mission.Priority.ToString(),
                    TargetAddress = activeAssignment.Mission.TargetAddress,
                    AcceptedAt = activeAssignment.Mission.AcceptedAt
                }
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("nearby")]
    public async Task<ActionResult<List<NearbyVehicleResponse>>> GetNearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int count = 5,
        CancellationToken ct = default)
    {
        var nearby = await gpsService.FindNearbyAvailableVehiclesAsync(lat, lng, count, ct);

        var vehicleIds = nearby.Select(n => n.VehicleId).ToList();
        var vehicles = await db.PatrolVehicles
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var result = nearby.Select(n => new NearbyVehicleResponse
        {
            VehicleId = n.VehicleId,
            CallSign = vehicles.TryGetValue(n.VehicleId, out var v) ? v.CallSign : string.Empty,
            Latitude = n.Lat,
            Longitude = n.Lng,
            DistanceKm = Math.Round(n.Distance, 2)
        }).ToList();

        return Ok(result);
    }

    [HttpPost("{id:guid}/sos")]
    public async Task<ActionResult<VehicleSosResponse>> TriggerSos(Guid id, CancellationToken ct)
    {
        var vehicle = await db.PatrolVehicles
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        var triggeredAt = DateTime.UtcNow;

        // Notifier les opérateurs via SignalR
        var sosPayload = new VehicleSosResponse
        {
            VehicleId   = vehicle.Id,
            CallSign    = vehicle.CallSign,
            Latitude    = vehicle.LastLatitude,
            Longitude   = vehicle.LastLongitude,
            TriggeredAt = triggeredAt
        };

        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("VehicleSOSAlert", sosPayload, ct);

        // Envoyer un email aux managers du tenant
        var subject = $"🚨 ALERTE SOS — Véhicule {vehicle.CallSign}";
        var body = $"""
            <h2 style="color:red">🚨 ALERTE SOS</h2>
            <p>Le véhicule <strong>{vehicle.CallSign}</strong> a déclenché une alerte SOS.</p>
            <p><strong>Heure :</strong> {triggeredAt.ToLocalTime():dd/MM/yyyy HH:mm:ss}</p>
            {(vehicle.LastLatitude.HasValue && vehicle.LastLongitude.HasValue
                ? $"<p><strong>Dernière position :</strong> {vehicle.LastLatitude:F5}, {vehicle.LastLongitude:F5}</p>"
                : "<p><em>Position non disponible.</em></p>")}
            <p>Contactez immédiatement le véhicule et envoyez des renforts.</p>
            """;

        await emailService.SendToManagersAsync(TenantId, subject, body, ct);

        return Ok(sosPayload);
    }

    private static VehicleResponse MapToResponse(PatrolVehicle v) => new()
    {
        Id = v.Id,
        CallSign = v.CallSign,
        LicensePlate = v.LicensePlate,
        Status = v.Status,
        LastLatitude = v.LastLatitude,
        LastLongitude = v.LastLongitude,
        LastPositionUpdate = v.LastPositionUpdate,
        OfficerNames = v.Officers
            .Where(o => o.IsActive)
            .Select(o => o.User.FullName)
            .ToList(),
        BeaconUuid = v.BeaconUuid,
        AssignedGeoZoneId = v.AssignedGeoZoneId,
        Capacity = v.Capacity,
        Indicatif = v.Indicatif,
        PatrolType = v.PatrolType,
        SessionStartedAt = v.SessionStartedAt,
        Crew = v.Officers
            .Where(o => o.IsActive)
            .Select(o => new CrewMemberInfo
            {
                UserId = o.UserId,
                FullName = o.User.FullName,
                BadgeNumber = o.User.BadgeNumber,
                IsLeader = o.IsLeader
            }).ToList()
    };
}
