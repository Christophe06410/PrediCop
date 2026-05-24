using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;
using PrediCop.Api.Hubs;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PatrolController(
    AppDbContext db,
    IHubContext<PoliceHub> hubContext) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);
    private Guid UserId   => Guid.Parse(User.FindFirst("userId")!.Value);

    /// <summary>
    /// Retourne la liste des véhicules disponibles pour le chef de patrouille.
    /// </summary>
    [HttpGet("vehicles")]
    [Authorize(Roles = "PatrolLeader,Admin,Manager")]
    public async Task<ActionResult<List<VehicleResponse>>> GetPatrolVehicles(CancellationToken ct)
    {
        var vehicles = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .Where(v => v.TenantId == TenantId)
            .OrderBy(v => v.CallSign)
            .ToListAsync(ct);

        return Ok(vehicles.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// Retourne la liste des agents (PatrolAgent) disponibles pour composer une patrouille.
    /// </summary>
    [HttpGet("available-agents")]
    [Authorize(Roles = "PatrolLeader,Admin,Manager")]
    public async Task<ActionResult<List<AvailableAgentDto>>> GetAvailableAgents(CancellationToken ct)
    {
        var agents = await db.Users
            .Where(u => u.TenantId == TenantId && u.IsActive && !u.IsDeleted
                     && (u.Role == UserRole.PatrolAgent || u.Role == UserRole.Officer))
            .OrderBy(u => u.LastName)
            .Select(u => new AvailableAgentDto
            {
                Id = u.Id,
                FullName = u.FullName,
                BadgeNumber = u.BadgeNumber,
                Role = u.Role.ToString()
            })
            .ToListAsync(ct);

        return Ok(agents);
    }

    /// <summary>
    /// Le chef de patrouille active sa patrouille sur un véhicule.
    /// Définit l'indicatif, le type, et la composition de l'équipe.
    /// </summary>
    [HttpPost("{vehicleId:guid}/activate")]
    [Authorize(Roles = "PatrolLeader,Admin,Manager")]
    public async Task<IActionResult> ActivatePatrol(
        Guid vehicleId,
        [FromBody] PatrolActivateRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Indicatif))
            return Problem(title: "L'indicatif est requis", statusCode: 400);

        var vehicle = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
                .ThenInclude(o => o.User)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        // Désactiver les assignations précédentes
        foreach (var off in vehicle.Officers)
        {
            off.IsActive = false;
            off.UnassignedAt = DateTime.UtcNow;
        }

        // Marquer le chef de patrouille (l'appelant)
        var leaderAssignment = new VehicleOfficer
        {
            VehicleId = vehicleId,
            UserId = UserId,
            IsActive = true,
            IsLeader = true,
            AssignedAt = DateTime.UtcNow
        };
        db.VehicleOfficers.Add(leaderAssignment);

        // Ajouter les agents
        foreach (var agentId in request.AgentIds.Distinct())
        {
            if (agentId == UserId) continue; // chef déjà ajouté
            var agentExists = await db.Users
                .AnyAsync(u => u.Id == agentId && u.TenantId == TenantId && u.IsActive && !u.IsDeleted, ct);
            if (!agentExists) continue;

            db.VehicleOfficers.Add(new VehicleOfficer
            {
                VehicleId = vehicleId,
                UserId = agentId,
                IsActive = true,
                IsLeader = false,
                AssignedAt = DateTime.UtcNow
            });
        }

        // Mettre à jour la session sur le véhicule
        vehicle.Indicatif = request.Indicatif.Trim();
        vehicle.PatrolType = request.PatrolType;
        vehicle.SessionStartedAt = DateTime.UtcNow;
        vehicle.Status = VehicleStatus.Available;

        await db.SaveChangesAsync(ct);

        // Notifier les opérateurs
        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("PatrolActivated", new
            {
                vehicleId,
                indicatif = vehicle.Indicatif,
                patrolType = vehicle.PatrolType!.ToString()
            }, ct);

        return Ok(MapToResponse(vehicle));
    }

    /// <summary>
    /// Le chef de patrouille désactive sa patrouille (fin de service).
    /// </summary>
    [HttpPost("{vehicleId:guid}/deactivate")]
    [Authorize(Roles = "PatrolLeader,Admin,Manager")]
    public async Task<IActionResult> DeactivatePatrol(Guid vehicleId, CancellationToken ct)
    {
        var vehicle = await db.PatrolVehicles
            .Include(v => v.Officers.Where(o => o.IsActive))
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        foreach (var off in vehicle.Officers)
        {
            off.IsActive = false;
            off.UnassignedAt = DateTime.UtcNow;
        }

        vehicle.Indicatif = null;
        vehicle.PatrolType = null;
        vehicle.SessionStartedAt = null;
        vehicle.Status = VehicleStatus.Offline;

        await db.SaveChangesAsync(ct);

        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("PatrolDeactivated", new { vehicleId }, ct);

        return NoContent();
    }

    /// <summary>
    /// Met à jour la position GPS individuelle de l'agent/chef appelant.
    /// </summary>
    [HttpPost("my-position")]
    public async Task<IActionResult> UpdateMyPosition(
        [FromBody] AgentPositionUpdate request,
        CancellationToken ct)
    {
        var user = await db.Users.FindAsync([UserId], ct);
        if (user is null || user.IsDeleted)
            return Problem(title: "Utilisateur non trouvé", statusCode: 404);

        user.LastLatitude = request.Latitude;
        user.LastLongitude = request.Longitude;
        user.LastPositionUpdate = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // Notifier les opérateurs via SignalR
        var vehicleId = await db.VehicleOfficers
            .Where(vo => vo.UserId == UserId && vo.IsActive)
            .Select(vo => (Guid?)vo.VehicleId)
            .FirstOrDefaultAsync(ct);

        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("AgentPositionUpdated", new
            {
                userId = UserId,
                latitude = request.Latitude,
                longitude = request.Longitude,
                updatedAt = DateTime.UtcNow,
                vehicleId
            }, ct);

        return NoContent();
    }

    /// <summary>
    /// Retourne les positions GPS individuelles de tous les agents et chefs actifs.
    /// </summary>
    [HttpGet("live-agents")]
    public async Task<ActionResult<List<AgentPositionDto>>> GetLiveAgents(CancellationToken ct)
    {
        // Agents actuellement assignés à un véhicule actif (session en cours)
        var patrolAgents = await db.VehicleOfficers
            .Include(vo => vo.User)
            .Include(vo => vo.Vehicle)
            .Where(vo => vo.IsActive
                      && vo.Vehicle.TenantId == TenantId
                      && vo.Vehicle.SessionStartedAt != null
                      && vo.User.LastLatitude != null
                      && vo.User.LastLongitude != null)
            .ToListAsync(ct);

        var result = patrolAgents.Select(vo => new AgentPositionDto
        {
            UserId = vo.UserId,
            FullName = vo.User.FullName,
            BadgeNumber = vo.User.BadgeNumber,
            IsLeader = vo.IsLeader,
            Latitude = vo.User.LastLatitude!.Value,
            Longitude = vo.User.LastLongitude!.Value,
            UpdatedAt = vo.User.LastPositionUpdate ?? DateTime.UtcNow,
            PatrolIndicatif = vo.Vehicle.Indicatif
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
        BeaconUuid = v.BeaconUuid,
        AssignedGeoZoneId = v.AssignedGeoZoneId,
        Capacity = v.Capacity,
        Indicatif = v.Indicatif,
        PatrolType = v.PatrolType,
        SessionStartedAt = v.SessionStartedAt,
        OfficerNames = v.Officers
            .Where(o => o.IsActive)
            .Select(o => o.User.FullName)
            .ToList(),
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

public class AvailableAgentDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string BadgeNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
