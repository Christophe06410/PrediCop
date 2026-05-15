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
        [FromBody] PatrolRequest request,
        CancellationToken ct)
    {
        var street = await db.Streets
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == TenantId, ct);

        if (street is null)
            return Problem(title: "Rue non trouvée", statusCode: 404);

        await streetRiskService.RecordPatrolAsync(id, request.VehicleId, ct);

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
