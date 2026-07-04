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
    IHubContext<PoliceHub> hubContext,
    IFlowLogService flowLog,
    ILogger<CallsController> logger) : ControllerBase
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

        var callEntities = await query
            .OrderByDescending(c => c.ReceivedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return Ok(new PagedResult<CallResponse>
        {
            Items = callEntities.Select(MapToResponse).ToList(),
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
            .Include(c => c.Missions)
                .ThenInclude(m => m.TrackingDocuments)
                .ThenInclude(d => d.Entries)
            .Include(c => c.Reports)
                .ThenInclude(r => r.Author)
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
            CallerName = request.CallerName ?? string.Empty,
            CallerPhone = request.CallerPhone ?? string.Empty,
            IncidentDescription = request.IncidentDescription ?? string.Empty,
            IncidentCategory = request.IncidentCategory ?? string.Empty,
            IncidentAddress = request.IncidentAddress ?? string.Empty,
            IncidentAddressComplement = request.IncidentAddressComplement,
            IncidentLatitude = request.IncidentLatitude,
            IncidentLongitude = request.IncidentLongitude,
            ThirdParties = request.ThirdParties,
            Notes = request.Notes,
            InternalNotes = request.InternalNotes,
            OperatorId = UserId,
            Status = request.Status ?? CallStatus.Open,
            Priority = request.Priority,
            CallDurationSeconds = request.CallDurationSeconds,
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

            var proposedAssignment = response.Assignments
                .OrderByDescending(a => a.ProposalOrder)
                .FirstOrDefault(a => a.Status == MissionStatus.Proposed);

            if (proposedAssignment is not null)
            {
                await hubContext.Clients
                    .Group($"vehicle_{proposedAssignment.VehicleId}")
                    .SendAsync("MissionProposed", proposedAssignment, ct);
                logger.LogInformation("[CreateMission] Auto-dispatch : MissionProposed → groupe vehicle_{VehicleId} pour mission {MissionId}",
                    proposedAssignment.VehicleId, mission.Id);
                flowLog.Log("Server", "Information", "Dispatch",
                    $"Auto-dispatch : MissionProposed → vehicle_{proposedAssignment.VehicleId} ({proposedAssignment.VehicleCallSign}) pour {mission.Reference}",
                    TenantId, null, $"missionId={mission.Id};assignmentId={proposedAssignment.Id}");
            }
            else
            {
                logger.LogInformation("[CreateMission] Aucun véhicule proposé automatiquement pour la mission {MissionId} (reste en attente de dispatch manuel)",
                    mission.Id);
                flowLog.Log("Server", "Warning", "Dispatch",
                    $"Auto-dispatch : aucun véhicule proposé pour {mission.Reference} (dispatch manuel requis)",
                    TenantId, null, $"missionId={mission.Id}");
            }

            // Notifier les opérateurs du tenant pour rafraîchir la liste des missions / équipages en temps réel.
            await hubContext.Clients
                .Group($"operators_{TenantId}")
                .SendAsync("MissionStatusChanged", response, ct);
            logger.LogInformation("[CreateMission] MissionStatusChanged → groupe operators_{TenantId} pour mission {MissionId}",
                TenantId, mission.Id);

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
        CallDurationSeconds = c.CallDurationSeconds,
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
        response.Reports = c.Reports
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .Select(MapReportToResponse)
            .ToList();
        return response;
    }

    private static CallReportResponse MapReportToResponse(CallReport r) => new()
    {
        Id = r.Id,
        CallId = r.CallId,
        CallReference = r.Call?.Reference ?? string.Empty,
        Type = r.Type.ToString(),
        TypeLabel = r.Type switch
        {
            ReportType.Information     => "Rapport d'information",
            ReportType.Intervention    => "Rapport d'intervention",
            ReportType.CustodyTransfer => "Rapport de mise à disposition",
            ReportType.FormalRecord    => "Procès-verbal",
            _                          => r.Type.ToString()
        },
        Title = r.Title,
        Body = r.Body,
        AuthorId = r.AuthorId,
        AuthorName = r.Author?.FullName ?? string.Empty,
        Recipients = (int)r.Recipients,
        RecipientLabels = GetRecipientLabels(r.Recipients),
        IsDraft = r.IsDraft,
        FinalizedAt = r.FinalizedAt,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };

    private static string[] GetRecipientLabels(ReportRecipient recipients)
    {
        var labels = new List<string>();
        if (recipients.HasFlag(ReportRecipient.Mayor))      labels.Add("Maire");
        if (recipients.HasFlag(ReportRecipient.Prosecutor)) labels.Add("Procureur");
        if (recipients.HasFlag(ReportRecipient.Prefecture)) labels.Add("Préfecture");
        if (recipients.HasFlag(ReportRecipient.Hierarchy))  labels.Add("Hiérarchie");
        if (recipients.HasFlag(ReportRecipient.Department)) labels.Add("Service");
        return [.. labels];
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
        LocationDetail = m.LocationDetail,
        NarrativeReport = m.NarrativeReport,
        AcceptedAt = m.AcceptedAt,
        CompletedAt = m.CompletedAt,
        CompletionReport = m.CompletionReport,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        AssignedVehicleIndicatif = m.Assignments
            .Where(a => a.Status == MissionStatus.Accepted
                     || a.Status == MissionStatus.InProgress
                     || a.Status == MissionStatus.Proposed)
            .Select(a => a.Vehicle?.Indicatif)
            .FirstOrDefault(s => !string.IsNullOrEmpty(s)),
        Assignments = m.Assignments.Select(a => new MissionAssignmentResponse
        {
            Id = a.Id,
            MissionId = a.MissionId,
            VehicleId = a.VehicleId,
            VehicleCallSign = a.Vehicle?.CallSign ?? string.Empty,
            VehicleIndicatif = a.Vehicle?.Indicatif,
            ProposalOrder = a.ProposalOrder,
            Status = a.Status,
            ProposedAt = a.ProposedAt,
            RespondedAt = a.RespondedAt,
            RefusalReason = a.RefusalReason,
            DistanceAtProposal = a.DistanceAtProposal
        }).ToList(),
        TrackingDocuments = m.TrackingDocuments.Select(d => new TrackingDocumentSummary
        {
            Id = d.Id,
            Reference = d.Reference,
            Type = d.Type.ToString(),
            Status = d.Status.ToString(),
            Title = d.Title,
            CreatedAt = d.CreatedAt,
            EntryCount = d.Entries?.Count ?? 0
        }).ToList()
    };
}
