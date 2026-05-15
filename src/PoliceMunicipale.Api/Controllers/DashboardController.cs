using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PoliceMunicipale.Core.DTOs;
using PoliceMunicipale.Core.Enums;
using PoliceMunicipale.Infrastructure.Data;

namespace PoliceMunicipale.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStats>> GetStats(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var totalCallsToday = await db.Calls
            .CountAsync(c => c.TenantId == TenantId && c.ReceivedAt.Date == today, ct);

        var openCalls = await db.Calls
            .CountAsync(c => c.TenantId == TenantId
                && (c.Status == CallStatus.Open || c.Status == CallStatus.InProgress), ct);

        var activeMissions = await db.Missions
            .CountAsync(m => m.TenantId == TenantId
                && (m.Status == MissionStatus.Pending
                    || m.Status == MissionStatus.Proposed
                    || m.Status == MissionStatus.Accepted
                    || m.Status == MissionStatus.InProgress), ct);

        var completedToday = await db.Missions
            .CountAsync(m => m.TenantId == TenantId
                && m.Status == MissionStatus.Completed
                && m.CompletedAt.HasValue
                && m.CompletedAt.Value.Date == today, ct);

        var availableVehicles = await db.PatrolVehicles
            .CountAsync(v => v.TenantId == TenantId && v.Status == VehicleStatus.Available, ct);

        var totalVehicles = await db.PatrolVehicles
            .CountAsync(v => v.TenantId == TenantId, ct);

        var highRiskStreets = await db.Streets
            .CountAsync(s => s.TenantId == TenantId && s.CurrentRiskScore >= 70, ct);

        // Average response time for accepted missions today
        var acceptedMissions = await db.Missions
            .Include(m => m.Assignments)
            .Where(m => m.TenantId == TenantId
                && m.AcceptedAt.HasValue
                && m.CreatedAt.Date == today)
            .ToListAsync(ct);

        double avgResponseTime = 0;
        if (acceptedMissions.Count > 0)
        {
            avgResponseTime = acceptedMissions
                .Select(m => (m.AcceptedAt!.Value - m.CreatedAt).TotalMinutes)
                .Average();
        }

        return Ok(new DashboardStats
        {
            TotalCallsToday = totalCallsToday,
            OpenCalls = openCalls,
            ActiveMissions = activeMissions,
            CompletedMissionsToday = completedToday,
            AvailableVehicles = availableVehicles,
            TotalVehicles = totalVehicles,
            HighRiskStreets = highRiskStreets,
            AverageMissionResponseTimeMinutes = Math.Round(avgResponseTime, 1)
        });
    }

    [HttpGet("vehicle-stats")]
    public async Task<ActionResult<List<VehicleStats>>> GetVehicleStats(CancellationToken ct)
    {
        var vehicles = await db.PatrolVehicles
            .Where(v => v.TenantId == TenantId)
            .ToListAsync(ct);

        var assignments = await db.MissionAssignments
            .Include(a => a.Vehicle)
            .Where(a => a.Vehicle.TenantId == TenantId)
            .ToListAsync(ct);

        var stats = vehicles.Select(v =>
        {
            var vehicleAssignments = assignments.Where(a => a.VehicleId == v.Id).ToList();
            return new VehicleStats
            {
                VehicleId = v.Id,
                CallSign = v.CallSign,
                TotalProposed = vehicleAssignments.Count,
                TotalAccepted = vehicleAssignments.Count(a =>
                    a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Completed),
                TotalRefused = vehicleAssignments.Count(a => a.Status == MissionStatus.Refused),
                TotalCompleted = vehicleAssignments.Count(a => a.Status == MissionStatus.Completed)
            };
        }).ToList();

        return Ok(stats);
    }

    [HttpGet("missions-by-hour")]
    public async Task<ActionResult<List<MissionStats>>> GetMissionsByHour(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var missions = await db.Missions
            .Where(m => m.TenantId == TenantId && m.CreatedAt.Date == today)
            .Select(m => new { m.CreatedAt.Hour, m.Status })
            .ToListAsync(ct);

        var stats = Enumerable.Range(0, 24).Select(hour => new MissionStats
        {
            Hour = hour,
            MissionCount = missions.Count(m => m.Hour == hour),
            CompletedCount = missions.Count(m => m.Hour == hour && m.Status == MissionStatus.Completed)
        }).ToList();

        return Ok(stats);
    }
}
