using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PrediCop.Api.Services;
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
    IHubContext<PoliceHub> hubContext,
    StreetRiskComputeService computeService) : ControllerBase
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

    [HttpGet("risk-events")]
    public async Task<ActionResult<List<RiskEventResponse>>> GetRiskEvents(
        [FromQuery] Guid? streetId,
        [FromQuery] bool? active,
        CancellationToken ct)
    {
        var query = db.StreetRiskEvents
            .Include(e => e.Street)
            .Where(e => e.TenantId == TenantId);

        if (streetId.HasValue)
            query = query.Where(e => e.StreetId == streetId.Value);

        if (active == true)
            query = query.Where(e => e.ExpiresAt > DateTime.UtcNow);

        var events = await query
            .OrderByDescending(e => e.EventDate)
            .ToListAsync(ct);

        var result = events.Select(e => new RiskEventResponse(
            e.Id,
            e.StreetId,
            e.Street.Name,
            e.Street.District ?? string.Empty,
            e.Title,
            e.Description,
            e.RiskPoints,
            e.EventDate,
            e.ExpiresAt,
            e.Source,
            e.ExpiresAt > DateTime.UtcNow
        )).ToList();

        return Ok(result);
    }

    [HttpPut("{streetId:guid}/risk-events/{eventId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<StreetResponse>> UpdateRiskEvent(
        Guid streetId,
        Guid eventId,
        [FromBody] UpdateRiskEventRequest request,
        CancellationToken ct)
    {
        var street = await db.Streets
            .FirstOrDefaultAsync(s => s.Id == streetId && s.TenantId == TenantId, ct);

        if (street is null)
            return Problem(title: "Rue non trouvée", statusCode: 404);

        var riskEvent = await db.StreetRiskEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.StreetId == streetId && e.TenantId == TenantId, ct);

        if (riskEvent is null)
            return Problem(title: "Événement non trouvé", statusCode: 404);

        riskEvent.Title = request.Title;
        riskEvent.Description = request.Description;
        riskEvent.RiskPoints = request.RiskPoints;
        riskEvent.EventDate = request.EventDate;
        riskEvent.ExpiresAt = request.ExpiresAt;
        riskEvent.Source = request.Source;

        await db.SaveChangesAsync(ct);

        var newRisk = await streetRiskService.CalculateCurrentRiskScoreAsync(streetId, ct);
        street.CurrentRiskScore = newRisk;
        await db.SaveChangesAsync(ct);

        var response = MapToResponse(street);

        await hubContext.Clients
            .Group($"tenant_{TenantId}")
            .SendAsync("StreetRiskUpdated", response, ct);

        return Ok(response);
    }

    [HttpDelete("{streetId:guid}/risk-events/{eventId:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> DeleteRiskEvent(
        Guid streetId,
        Guid eventId,
        CancellationToken ct)
    {
        var street = await db.Streets
            .FirstOrDefaultAsync(s => s.Id == streetId && s.TenantId == TenantId, ct);

        if (street is null)
            return Problem(title: "Rue non trouvée", statusCode: 404);

        var riskEvent = await db.StreetRiskEvents
            .FirstOrDefaultAsync(e => e.Id == eventId && e.StreetId == streetId && e.TenantId == TenantId, ct);

        if (riskEvent is null)
            return Problem(title: "Événement non trouvé", statusCode: 404);

        riskEvent.IsDeleted = true;
        await db.SaveChangesAsync(ct);

        var newRisk = await streetRiskService.CalculateCurrentRiskScoreAsync(streetId, ct);
        street.CurrentRiskScore = newRisk;
        await db.SaveChangesAsync(ct);

        await hubContext.Clients
            .Group($"tenant_{TenantId}")
            .SendAsync("StreetRiskUpdated", MapToResponse(street), ct);

        return NoContent();
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

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<ActionResult<StreetResponse>> UpdateStreet(
        Guid id,
        [FromBody] UpdateStreetRequest request,
        CancellationToken ct)
    {
        var street = await db.Streets
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);

        if (street is null)
            return Problem(title: "Rue non trouvée", statusCode: 404);

        street.RiskGrowthRatePerHour = Math.Clamp(request.RiskGrowthRatePerHour, 0, 20);
        street.IsRiskLocked = request.IsRiskLocked;

        if (request.IsRiskLocked)
        {
            street.BaseRiskScore = Math.Clamp(request.BaseRiskScore, 0, 100);
            street.RiskAdjustment = null;
        }
        else
        {
            street.RiskAdjustment = request.RiskAdjustment;
            if (street.RiskAdjustment.HasValue)
                street.BaseRiskScore = Math.Clamp(street.ComputedBaseRiskScore + street.RiskAdjustment.Value, 0, 100);
        }

        await db.SaveChangesAsync(ct);

        return Ok(MapToResponse(street));
    }

    [HttpPost("recompute-risks")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecomputeRisks(CancellationToken ct)
    {
        await computeService.ComputeForTenantAsync(TenantId, refreshDensity: false, ct);
        return Ok(new { message = "Recalcul des scores de risque terminé." });
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
        ComputedBaseRiskScore = s.ComputedBaseRiskScore,
        IsRiskLocked = s.IsRiskLocked,
        RiskAdjustment = s.RiskAdjustment,
        RiskGrowthRatePerHour = s.RiskGrowthRatePerHour,
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
