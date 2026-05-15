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
public class MissionsController(
    AppDbContext db,
    IMissionService missionService,
    IHubContext<PoliceHub> hubContext) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    [HttpGet]
    public async Task<ActionResult<PagedResult<MissionResponse>>> GetMissions(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] MissionStatus? status = null,
        CancellationToken ct = default)
    {
        var query = db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .Where(m => m.TenantId == TenantId);

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        var totalCount = await query.CountAsync(ct);

        var missions = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return Ok(new PagedResult<MissionResponse>
        {
            Items = missions.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = size
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MissionResponse>> GetMission(Guid id, CancellationToken ct)
    {
        var mission = await db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        return Ok(MapToResponse(mission));
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<MissionResponse>>> GetActiveMissions(CancellationToken ct)
    {
        var missions = await db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .Where(m => m.TenantId == TenantId
                && (m.Status == MissionStatus.Pending
                    || m.Status == MissionStatus.Proposed
                    || m.Status == MissionStatus.Accepted
                    || m.Status == MissionStatus.InProgress))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return Ok(missions.Select(MapToResponse).ToList());
    }

    [HttpPost("{id:guid}/propose")]
    public async Task<ActionResult<MissionAssignmentResponse>> Propose(Guid id, CancellationToken ct)
    {
        var mission = await db.Missions
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        try
        {
            var assignment = await missionService.ProposeToNextVehicleAsync(id, ct);

            await db.Entry(assignment).Reference(a => a.Vehicle).LoadAsync(ct);

            var assignmentResponse = MapAssignmentToResponse(assignment);

            await hubContext.Clients
                .Group($"vehicle_{assignment.VehicleId}")
                .SendAsync("MissionProposed", assignmentResponse, ct);

            return Ok(assignmentResponse);
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors de la proposition", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id:guid}/assignments/{assignmentId:guid}/accept")]
    public async Task<ActionResult<MissionAssignmentResponse>> Accept(Guid id, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.MissionAssignments
            .Include(a => a.Mission).ThenInclude(m => m.Call)
            .Include(a => a.Mission).ThenInclude(m => m.Assignments).ThenInclude(a2 => a2.Vehicle)
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.MissionId == id, ct);

        if (assignment is null)
            return Problem(title: "Assignation non trouvée", statusCode: 404);

        try
        {
            var updated = await missionService.RespondToProposalAsync(assignmentId, true, null, ct);
            var missionResponse = MapToResponse(assignment.Mission);

            await hubContext.Clients
                .Group($"operators_{TenantId}")
                .SendAsync("MissionStatusChanged", missionResponse, ct);

            return Ok(MapAssignmentToResponse(updated));
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors de l'acceptation", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id:guid}/assignments/{assignmentId:guid}/refuse")]
    public async Task<ActionResult<MissionAssignmentResponse>> Refuse(
        Guid id, Guid assignmentId,
        [FromBody] RefuseMissionRequest request,
        CancellationToken ct)
    {
        var assignment = await db.MissionAssignments
            .Include(a => a.Mission).ThenInclude(m => m.Call)
            .Include(a => a.Mission).ThenInclude(m => m.Assignments).ThenInclude(a2 => a2.Vehicle)
            .Include(a => a.Vehicle)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.MissionId == id, ct);

        if (assignment is null)
            return Problem(title: "Assignation non trouvée", statusCode: 404);

        try
        {
            var updated = await missionService.RespondToProposalAsync(assignmentId, false, request.Reason, ct);
            var missionResponse = MapToResponse(assignment.Mission);

            await hubContext.Clients
                .Group($"operators_{TenantId}")
                .SendAsync("MissionStatusChanged", missionResponse, ct);

            return Ok(MapAssignmentToResponse(updated));
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors du refus", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<MissionResponse>> Complete(
        Guid id,
        [FromBody] CompleteMissionRequest request,
        CancellationToken ct)
    {
        var mission = await db.Missions
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        try
        {
            var completed = await missionService.CompleteMissionAsync(id, request.Report, ct);

            await db.Entry(completed).Reference(m => m.Call).LoadAsync(ct);
            await db.Entry(completed).Collection(m => m.Assignments).LoadAsync(ct);
            foreach (var a in completed.Assignments)
                await db.Entry(a).Reference(x => x.Vehicle).LoadAsync(ct);

            var response = MapToResponse(completed);

            await hubContext.Clients
                .Group($"operators_{TenantId}")
                .SendAsync("MissionStatusChanged", response, ct);

            return Ok(response);
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors de la complétion", detail: ex.Message, statusCode: 500);
        }
    }

    private static MissionResponse MapToResponse(Mission m) => new()
    {
        Id = m.Id,
        Reference = m.Reference,
        Status = m.Status,
        CallId = m.CallId,
        CallReference = m.Call?.Reference ?? string.Empty,
        TargetAddress = m.TargetAddress,
        TargetLatitude = m.TargetLatitude,
        TargetLongitude = m.TargetLongitude,
        BriefingText = m.BriefingText,
        AcceptedAt = m.AcceptedAt,
        CompletedAt = m.CompletedAt,
        CompletionReport = m.CompletionReport,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Assignments = m.Assignments.Select(MapAssignmentToResponse).ToList()
    };

    private static MissionAssignmentResponse MapAssignmentToResponse(MissionAssignment a) => new()
    {
        Id = a.Id,
        MissionId = a.MissionId,
        VehicleId = a.VehicleId,
        VehicleCallSign = a.Vehicle?.CallSign ?? string.Empty,
        ProposalOrder = a.ProposalOrder,
        Status = a.Status,
        ProposedAt = a.ProposedAt,
        RespondedAt = a.RespondedAt,
        RefusalReason = a.RefusalReason,
        DistanceAtProposal = a.DistanceAtProposal
    };
}
