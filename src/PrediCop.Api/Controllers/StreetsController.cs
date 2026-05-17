using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;
using PrediCop.Api.Hubs;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StreetsController(
    AppDbContext db,
    IStreetRiskService streetRiskService,
    IHubContext<PoliceHub> hubContext) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    [HttpPost]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<StreetResponse>> CreateStreet(
        [FromBody] CreateStreetRequest req, CancellationToken ct)
    {
        var street = new Street
        {
            TenantId = TenantId,
            Name = req.Name,
            District = req.District,
            City = req.City ?? "",
            BaseRiskScore = req.BaseRiskScore,
            CurrentRiskScore = req.CurrentRiskScore ?? req.BaseRiskScore,
            StartLatitude = req.StartLatitude,
            StartLongitude = req.StartLongitude,
            EndLatitude = req.EndLatitude,
            EndLongitude = req.EndLongitude
        };
        db.Streets.Add(street);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetStreets), MapToResponse(street));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteStreet(Guid id, CancellationToken ct)
    {
        var street = await db.Streets.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);
        if (street is null) return NotFound();
        db.Streets.Remove(street);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<StreetResponse>>> GetStreets(CancellationToken ct)
    {
        var streets = await db.Streets
            .Where(s => s.TenantId == TenantId)
            .OrderByDescending(s => s.CurrentRiskScore)
            .ToListAsync(ct);

        return Ok(streets.Select(MapToResponse).ToList());
    }

    [HttpGet("priority")]
    public async Task<ActionResult<List<StreetResponse>>> GetPriorityStreets(
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        var streets = await streetRiskService.GetStreetsOrderedByPriorityAsync(TenantId, count, ct);
        return Ok(streets.Select(MapToResponse).ToList());
    }

    [HttpPost("{id:guid}/patrol")]
    public async Task<ActionResult<StreetResponse>> RecordPatrol(
        Guid id,
        [FromBody] PatrolRequest? request,
        CancellationToken ct)
    {
        var street = await db.Streets
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);

        if (street is null)
            return Problem(title: "Rue non trouvée", statusCode: 404);

        // If no VehicleId provided, derive from the authenticated officer's assignment
        var vehicleId = (request?.VehicleId is Guid v && v != Guid.Empty) ? v : (Guid?)null;
        if (vehicleId is null)
        {
            var userId = Guid.Parse(User.FindFirst("userId")!.Value);
            vehicleId = await db.VehicleOfficers
                .Where(vo => vo.UserId == userId && vo.IsActive)
                .Select(vo => (Guid?)vo.VehicleId)
                .FirstOrDefaultAsync(ct);
        }

        if (vehicleId is null)
            return Problem(title: "Aucun véhicule assigné à cet agent", statusCode: 400);

        await streetRiskService.RecordPatrolAsync(id, vehicleId.Value, ct);

        await db.Entry(street).ReloadAsync(ct);

        var response = MapToResponse(street);

        await hubContext.Clients
            .Group($"tenant_{TenantId}")
            .SendAsync("StreetRiskUpdated", response, ct);

        return Ok(response);
    }

    [HttpPost("{id:guid}/risk-event")]
    public async Task<ActionResult<StreetResponse>> AddRiskEvent(
        Guid id,
        [FromBody] RiskEventRequest request,
        CancellationToken ct)
    {
        var street = await db.Streets
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);

        if (street is null)
            return Problem(title: "Rue non trouvée", statusCode: 404);

        var riskEvent = new StreetRiskEvent
        {
            TenantId = TenantId,
            StreetId = id,
            Title = request.Title,
            Description = request.Description,
            RiskPoints = request.RiskPoints,
            EventDate = request.EventDate,
            ExpiresAt = request.ExpiresAt,
            Source = request.Source
        };

        db.StreetRiskEvents.Add(riskEvent);
        await db.SaveChangesAsync(ct);

        var newRisk = await streetRiskService.CalculateCurrentRiskScoreAsync(id, ct);
        street.CurrentRiskScore = newRisk;
        await db.SaveChangesAsync(ct);

        var response = MapToResponse(street);

        await hubContext.Clients
            .Group($"tenant_{TenantId}")
            .SendAsync("StreetRiskUpdated", response, ct);

        return Ok(response);
    }

    [HttpPut("{id:guid}/base-risk")]
    public async Task<ActionResult<StreetResponse>> UpdateBaseRisk(
        Guid id,
        [FromBody] UpdateBaseRiskRequest request,
        CancellationToken ct)
    {
        var street = await db.Streets
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);

        if (street is null)
            return Problem(title: "Rue non trouvée", statusCode: 404);

        street.BaseRiskScore = request.BaseRiskScore;
        await db.SaveChangesAsync(ct);

        return Ok(MapToResponse(street));
    }

    private static StreetResponse MapToResponse(Street s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        District = s.District,
        City = s.City,
        StartLatitude = s.StartLatitude,
        StartLongitude = s.StartLongitude,
        EndLatitude = s.EndLatitude,
        EndLongitude = s.EndLongitude,
        GeoJson = s.GeoJson,
        BaseRiskScore = s.BaseRiskScore,
        CurrentRiskScore = s.CurrentRiskScore,
        LastPatrolledAt = s.LastPatrolledAt,
        PatrolIntervalHours = s.PatrolIntervalHours
    };
}

public class CreateStreetRequest
{
    public string Name { get; set; } = "";
    public string? District { get; set; }
    public string? City { get; set; }
    public int BaseRiskScore { get; set; } = 30;
    public int? CurrentRiskScore { get; set; }
    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double EndLatitude { get; set; }
    public double EndLongitude { get; set; }
}
