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
    IHubContext<PoliceHub> hubContext) : ControllerBase
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
            BeaconUuid = request.BeaconUuid
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
        AssignedGeoZoneId = v.AssignedGeoZoneId
    };
}
