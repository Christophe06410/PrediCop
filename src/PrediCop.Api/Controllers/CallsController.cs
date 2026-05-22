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
public class CallsController(
    AppDbContext db,
    IMissionService missionService,
    IHubContext<PoliceHub> hubContext) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);
    private Guid UserId => Guid.Parse(User.FindFirst("userId")!.Value);

    [HttpGet]
    public async Task<ActionResult<PagedResult<CallResponse>>> GetCalls(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] CallStatus? status = null,
        [FromQuery] DateTime? date = null,
        CancellationToken ct = default)
    {
        var query = db.Calls
            .Include(c => c.Operator)
            .Where(c => c.TenantId == TenantId);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(c => c.ReceivedAt.Date == day);
        }

        var totalCount = await query.CountAsync(ct);

        var calls = await query
            .OrderByDescending(c => c.ReceivedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(c => MapToResponse(c))
            .ToListAsync(ct);

        return Ok(new PagedResult<CallResponse>
        {
            Items = calls,
            TotalCount = totalCount,
            Page = page,
            PageSize = size
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CallResponse>> GetCall(Guid id, CancellationToken ct)
    {
        var call = await db.Calls
            .Include(c => c.Operator)
            .Include(c => c.Missions)
                .ThenInclude(m => m.Assignments)
                .ThenInclude(a => a.Vehicle)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);

        if (call is null)
            return Problem(title: "Appel non trouvé", statusCode: 404);

        return Ok(MapToResponseWithMissions(call));
    }

    [HttpPost]
    public async Task<ActionResult<CallResponse>> CreateCall([FromBody] CreateCallRequest request, CancellationToken ct)
    {
        var call = new Call
        {
            TenantId = TenantId,
            Reference = $"APP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
            CallerName = request.CallerName,
            CallerPhone = request.CallerPhone,
            IncidentDescription = request.IncidentDescription,
            IncidentCategory = request.IncidentCategory,
            IncidentAddress = request.IncidentAddress,
            IncidentAddressComplement = request.IncidentAddressComplement,
            IncidentLatitude = request.IncidentLatitude,
            IncidentLongitude = request.IncidentLongitude,
            ThirdParties = request.ThirdParties,
            Notes = request.Notes,
            InternalNotes = request.InternalNotes,
            OperatorId = UserId,
            Status = CallStatus.Open,
            Priority = request.Priority,
        };

        db.Calls.Add(call);
        await db.SaveChangesAsync(ct);

        await db.Entry(call).Reference(c => c.Operator).LoadAsync(ct);

        var response = MapToResponse(call);

        // Notifier les opérateurs du tenant du nouvel appel
        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("NewCallReceived", response, ct);

        return CreatedAtAction(nameof(GetCall), new { id = call.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CallResponse>> UpdateCall(Guid id, [FromBody] UpdateCallRequest request, CancellationToken ct)
    {
        var call = await db.Calls
            .Include(c => c.Operator)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);

        if (call is null)
            return Problem(title: "Appel non trouvé", statusCode: 404);

        if (request.CallerName is not null) call.CallerName = request.CallerName;
        if (request.CallerPhone is not null) call.CallerPhone = request.CallerPhone;
        if (request.IncidentDescription is not null) call.IncidentDescription = request.IncidentDescription;
        if (request.IncidentCategory is not null) call.IncidentCategory = request.IncidentCategory;
        if (request.IncidentAddress is not null) call.IncidentAddress = request.IncidentAddress;
        if (request.IncidentAddressComplement is not null) call.IncidentAddressComplement = request.IncidentAddressComplement;
        if (request.IncidentLatitude.HasValue) call.IncidentLatitude = request.IncidentLatitude;
        if (request.IncidentLongitude.HasValue) call.IncidentLongitude = request.IncidentLongitude;
        if (request.ThirdParties is not null) call.ThirdParties = request.ThirdParties;
        if (request.Notes is not null) call.Notes = request.Notes;
        if (request.InternalNotes is not null) call.InternalNotes = request.InternalNotes;
        if (request.Priority.HasValue) call.Priority = request.Priority.Value;

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(call));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<CallResponse>> CloseCall(Guid id, [FromBody] CloseCallRequest request, CancellationToken ct)
    {
        var call = await db.Calls
            .Include(c => c.Operator)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);

        if (call is null)
            return Problem(title: "Appel non trouvé", statusCode: 404);

        if (call.Status == CallStatus.Closed)
            return Problem(title: "L'appel est déjà fermé", statusCode: 400);

        call.Status = CallStatus.Closed;
        if (request.InternalNotes is not null)
            call.InternalNotes = request.InternalNotes;

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(call));
    }

    [HttpPost("{id:guid}/create-mission")]
    public async Task<ActionResult<MissionResponse>> CreateMission(Guid id, CancellationToken ct)
    {
        var call = await db.Calls
            .Include(c => c.Missions)
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == TenantId, ct);

        if (call is null)
            return Problem(title: "Appel non trouvé", statusCode: 404);

        // Bloquer uniquement si une mission est déjà active (proposée, acceptée, en cours)
        if (call.Missions.Any(m =>
                m.Status == MissionStatus.Proposed ||
                m.Status == MissionStatus.Accepted ||
                m.Status == MissionStatus.InProgress))
            return Problem(title: "Une mission est déjà active sur cet appel", statusCode: 400);

        try
        {
            var mission = await missionService.CreateMissionFromCallAsync(call.Id, ct);

            var response = await BuildMissionResponseAsync(mission.Id, ct);
            return CreatedAtAction("GetMission", "Missions", new { id = mission.Id }, response);
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors de la création de la mission", detail: ex.Message, statusCode: 500);
        }
    }

    private async Task<MissionResponse> BuildMissionResponseAsync(Guid missionId, CancellationToken ct)
    {
        var mission = await db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments)
                .ThenInclude(a => a.Vehicle)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct);

        return MapMissionToResponse(mission!);
    }

    private static CallResponse MapToResponse(Call c) => new()
    {
        Id = c.Id,
        Reference = c.Reference,
        ReceivedAt = c.ReceivedAt,
        Status = c.Status,
        Priority = c.Priority,
        CallerName = c.CallerName,
        CallerPhone = c.CallerPhone,
        IncidentDescription = c.IncidentDescription,
        IncidentCategory = c.IncidentCategory,
        IncidentAddress = c.IncidentAddress,
        IncidentAddressComplement = c.IncidentAddressComplement,
        IncidentLatitude = c.IncidentLatitude,
        IncidentLongitude = c.IncidentLongitude,
        ThirdParties = c.ThirdParties,
        Notes = c.Notes,
        InternalNotes = c.InternalNotes,
        OperatorId = c.OperatorId,
        OperatorName = c.Operator?.FullName ?? string.Empty,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    private static CallResponse MapToResponseWithMissions(Call c)
    {
        var response = MapToResponse(c);
        response.Missions = c.Missions
            .OrderBy(m => m.CreatedAt)
            .Select(MapMissionToResponse)
            .ToList();
        return response;
    }

    private static MissionResponse MapMissionToResponse(Mission m) => new()
    {
        Id = m.Id,
        Reference = m.Reference,
        Status = m.Status,
        Priority = m.Priority,
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
        Assignments = m.Assignments.Select(a => new MissionAssignmentResponse
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
        }).ToList()
    };
}
