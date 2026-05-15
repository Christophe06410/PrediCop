using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PoliceMunicipale.Core.DTOs;
using PoliceMunicipale.Core.Entities;
using PoliceMunicipale.Core.Enums;
using PoliceMunicipale.Core.Interfaces;
using PoliceMunicipale.Infrastructure.Data;
using PoliceMunicipale.Api.Hubs;

namespace PoliceMunicipale.Api.Controllers;

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
            .ToList()
    };
}
