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
public class MissionsController(
    AppDbContext db,
    IMissionService missionService,
    IHubContext<PoliceHub> hubContext,
    IEmailService emailService,
    IPushNotificationService pushService,
    IFlowLogService flowLog,
    ILogger<MissionsController> logger) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);
    private Guid? CurrentUserId => Guid.TryParse(User.FindFirst("userId")?.Value, out var u) ? u : null;

    [HttpGet]
    public async Task<ActionResult<PagedResult<MissionResponse>>> GetMissions(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        [FromQuery] MissionStatus? status = null,
        [FromQuery] Guid? vehicleId = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken ct = default)
    {
        var query = db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .Where(m => m.TenantId == TenantId);

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        if (vehicleId.HasValue)
            query = query.Where(m => m.Assignments.Any(a => a.VehicleId == vehicleId.Value));

        if (date.HasValue)
        {
            var day = date.Value.Date;
            if (status.HasValue && status.Value == MissionStatus.Completed)
                query = query.Where(m => m.CompletedAt.HasValue && m.CompletedAt.Value.Date == day);
            else
                query = query.Where(m => m.CreatedAt.Date == day);
        }

        // Plage UTC explicite (prioritaire sur date) — permet au mobile d'envoyer la plage locale convertie en UTC
        if (dateFrom.HasValue)
        {
            if (status.HasValue && status.Value == MissionStatus.Completed)
                query = query.Where(m => m.CompletedAt >= dateFrom);
            else
                query = query.Where(m => m.CreatedAt >= dateFrom);
        }
        if (dateTo.HasValue)
        {
            if (status.HasValue && status.Value == MissionStatus.Completed)
                query = query.Where(m => m.CompletedAt < dateTo);
            else
                query = query.Where(m => m.CreatedAt < dateTo);
        }

        var totalCount = await query.CountAsync(ct);

        var missions = await query
            .OrderByDescending(m => m.Priority).ThenByDescending(m => m.CreatedAt)
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
                .ThenInclude(c => c.Missions)
                    .ThenInclude(sm => sm.Assignments).ThenInclude(a => a.Vehicle)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .Include(m => m.Intervenants.OrderBy(i => i.Order))
            .Include(m => m.MediaAttachments).ThenInclude(ma => ma.CreatedBy)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        var response = MapToResponse(mission);

        // Missions sœurs : toutes les missions du même appel sauf celle-ci
        if (mission.Call?.Missions is not null)
        {
            response.SiblingMissions = mission.Call.Missions
                .Where(sm => sm.Id != id)
                .OrderByDescending(sm => sm.CreatedAt)
                .Select(sm => new SiblingMissionResponse
                {
                    Id = sm.Id,
                    Reference = sm.Reference,
                    Status = sm.Status,
                    CreatedAt = sm.CreatedAt,
                    CompletedAt = sm.CompletedAt,
                    AssignedVehicleCallSign = sm.Assignments
                        .FirstOrDefault(a => a.Status == MissionStatus.Accepted)
                        ?.Vehicle?.CallSign
                })
                .ToList();
        }

        return Ok(response);
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<MissionResponse>>> GetActiveMissions(CancellationToken ct)
    {
        var baseQuery = db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .Where(m => m.TenantId == TenantId
                && (m.Status == MissionStatus.Pending
                    || m.Status == MissionStatus.InProgress));

        // Restrict to vehicle-specific view only for patrol roles (PatrolLeader / PatrolAgent).
        // BackOffice operators/managers also carry a vehicleId claim (from VehicleOfficer records)
        // but should see all tenant missions, not just those for their vehicle.
        var isPatrolRole = User.IsInRole("PatrolLeader") || User.IsInRole("PatrolAgent");
        var vehicleIdClaim = User.FindFirst("vehicleId")?.Value;
        if (isPatrolRole && Guid.TryParse(vehicleIdClaim, out var vehicleId))
        {
            var vehicle = await db.PatrolVehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == TenantId, ct);

            var isAvailable = vehicle?.Status == VehicleStatus.Available;

            baseQuery = baseQuery.Where(m =>
                // (a) missions already assigned to this vehicle (any active assignment status)
                m.Assignments.Any(a =>
                    a.VehicleId == vehicleId &&
                    (a.Status == MissionStatus.Proposed ||
                     a.Status == MissionStatus.Accepted ||
                     a.Status == MissionStatus.InProgress))
                ||
                // (b) unassigned Pending missions visible to available vehicles
                (isAvailable
                    && m.Status == MissionStatus.Pending
                    && !m.Assignments.Any(a =>
                        a.Status == MissionStatus.Proposed ||
                        a.Status == MissionStatus.Accepted ||
                        a.Status == MissionStatus.InProgress)));
        }

        var missions = await baseQuery
            .OrderByDescending(m => m.Priority).ThenBy(m => m.CreatedAt)
            .ToListAsync(ct);

        return Ok(missions.Select(MapToResponse).ToList());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MissionResponse>> UpdateMission(
        Guid id, [FromBody] UpdateMissionRequest request, CancellationToken ct)
    {
        var mission = await db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        if (request.BriefingText is not null) mission.BriefingText = request.BriefingText;
        if (request.TargetAddress is not null) mission.TargetAddress = request.TargetAddress;
        if (request.TargetLatitude.HasValue) mission.TargetLatitude = request.TargetLatitude.Value;
        if (request.TargetLongitude.HasValue) mission.TargetLongitude = request.TargetLongitude.Value;
        if (request.LocationDetail is not null) mission.LocationDetail = request.LocationDetail;
        if (request.NarrativeReport is not null) mission.NarrativeReport = request.NarrativeReport;
        if (request.CompletionReport is not null) mission.CompletionReport = request.CompletionReport;
        if (request.DispatchedAt.HasValue) mission.DispatchedAt = request.DispatchedAt;
        if (request.ArrivedAt.HasValue) mission.ArrivedAt = request.ArrivedAt;

        await db.SaveChangesAsync(ct);
        return Ok(MapToResponse(mission));
    }

    [HttpPost("{id:guid}/propose")]
    public async Task<ActionResult<MissionAssignmentResponse>> Propose(Guid id, CancellationToken ct)
    {
        var mission = await db.Missions
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        flowLog.Log("Server", "Information", "Dispatch",
            $"Propose demandé pour mission {mission.Reference}", TenantId, CurrentUserId, $"missionId={id}");

        try
        {
            // Si une proposition est déjà en attente, re-notifier le véhicule plutôt que d'en créer une nouvelle
            var existingProposal = await db.MissionAssignments
                .Include(a => a.Vehicle)
                .FirstOrDefaultAsync(a => a.MissionId == id && a.Status == MissionStatus.Proposed, ct);

            if (existingProposal is not null)
            {
                var existingResponse = MapAssignmentToResponse(existingProposal);
                await hubContext.Clients
                    .Group($"vehicle_{existingProposal.VehicleId}")
                    .SendAsync("MissionProposed", existingResponse, ct);
                logger.LogInformation("[Propose] Re-notification MissionProposed → groupe vehicle_{VehicleId} pour mission {MissionId}",
                    existingProposal.VehicleId, id);
                flowLog.Log("Server", "Information", "Dispatch",
                    $"Re-notification MissionProposed → vehicle_{existingProposal.VehicleId} ({existingProposal.Vehicle?.CallSign})",
                    TenantId, CurrentUserId, $"missionId={id};assignmentId={existingProposal.Id}");
                await NotifyOperatorsOfProposalAsync(id, ct);
                Response.Headers.Append("X-Dispatch-Resent", "true");
                return Ok(existingResponse);
            }

            var assignment = await missionService.ProposeToNextVehicleAsync(id, ct);

            await db.Entry(assignment).Reference(a => a.Vehicle).LoadAsync(ct);

            var assignmentResponse = MapAssignmentToResponse(assignment);

            await hubContext.Clients
                .Group($"vehicle_{assignment.VehicleId}")
                .SendAsync("MissionProposed", assignmentResponse, ct);
            logger.LogInformation("[Propose] MissionProposed → groupe vehicle_{VehicleId} pour mission {MissionId} (assignment {AssignmentId})",
                assignment.VehicleId, id, assignment.Id);
            flowLog.Log("Server", "Information", "Dispatch",
                $"MissionProposed → vehicle_{assignment.VehicleId} ({assignment.Vehicle?.CallSign}), dist={assignment.DistanceAtProposal:F1}km",
                TenantId, CurrentUserId, $"missionId={id};assignmentId={assignment.Id}");

            await NotifyOperatorsOfProposalAsync(id, ct);

            return Ok(assignmentResponse);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No available vehicle"))
        {
            // Dispatch épuisé — plus aucun véhicule disponible pour cette mission
            flowLog.Log("Server", "Warning", "Dispatch",
                $"Aucun véhicule dispatchable pour {mission.Reference} : {ex.Message}", TenantId, CurrentUserId, $"missionId={id}");
            try
            {
                var subject = $"[PrediCop] ⚠️ Mission {mission.Reference} sans véhicule disponible";
                var htmlBody = $"""
                    <html><body style="font-family:Arial,sans-serif;color:#1a2035;max-width:600px;margin:auto;">
                    <h2 style="color:#dc2626;border-bottom:2px solid #dc2626;padding-bottom:8px;">
                        PrediCop — ⚠️ Alerte dispatch
                    </h2>
                    <p>Aucun véhicule disponible n'a pu être trouvé pour la mission suivante.</p>
                    <table style="width:100%;border-collapse:collapse;margin:16px 0;">
                        <tr style="background:#fef2f2;">
                            <td style="padding:8px 12px;font-weight:bold;width:40%;">Référence mission</td>
                            <td style="padding:8px 12px;">{mission.Reference}</td>
                        </tr>
                        <tr>
                            <td style="padding:8px 12px;font-weight:bold;">Adresse</td>
                            <td style="padding:8px 12px;">{System.Net.WebUtility.HtmlEncode(mission.TargetAddress)}</td>
                        </tr>
                        <tr style="background:#fef2f2;">
                            <td style="padding:8px 12px;font-weight:bold;">Statut</td>
                            <td style="padding:8px 12px;">{mission.Status}</td>
                        </tr>
                        <tr>
                            <td style="padding:8px 12px;font-weight:bold;">Date</td>
                            <td style="padding:8px 12px;">{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</td>
                        </tr>
                    </table>
                    <p style="color:#dc2626;font-weight:bold;">Une intervention manuelle est nécessaire.</p>
                    <hr style="border:none;border-top:1px solid #e2e8f0;margin:24px 0;"/>
                    <small style="color:#64748b;">PrediCop — Police Municipale | Ce message est généré automatiquement.</small>
                    </body></html>
                    """;
                await emailService.SendToManagersAsync(TenantId, subject, htmlBody, ct);
            }
            catch (Exception)
            {
                // L'envoi email ne doit jamais faire échouer l'action principale
            }

            return Problem(title: "Aucun véhicule disponible", detail: ex.Message, statusCode: 503);
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
            var updated = await missionService.RespondToProposalAsync(assignmentId, true, null, null, ct);
            var missionResponse = MapToResponse(assignment.Mission);

            await hubContext.Clients
                .Group($"operators_{TenantId}")
                .SendAsync("MissionStatusChanged", missionResponse, ct);
            await BroadcastMissionStatusToVehiclesAsync(missionResponse, ct);

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
            var updated = await missionService.RespondToProposalAsync(assignmentId, false, request.ReasonCode, request.Reason, ct);
            var missionResponse = MapToResponse(assignment.Mission);

            await hubContext.Clients
                .Group($"operators_{TenantId}")
                .SendAsync("MissionStatusChanged", missionResponse, ct);
            await BroadcastMissionStatusToVehiclesAsync(missionResponse, ct);

            return Ok(MapAssignmentToResponse(updated));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No available vehicle"))
        {
            // Dispatch épuisé suite à un refus — tous les véhicules disponibles ont refusé
            try
            {
                var mission = assignment.Mission;
                var subject = $"[PrediCop] ⚠️ Mission {mission.Reference} sans véhicule disponible";
                var htmlBody = $"""
                    <html><body style="font-family:Arial,sans-serif;color:#1a2035;max-width:600px;margin:auto;">
                    <h2 style="color:#dc2626;border-bottom:2px solid #dc2626;padding-bottom:8px;">
                        PrediCop — ⚠️ Alerte dispatch
                    </h2>
                    <p>Tous les véhicules disponibles ont refusé la mission. Aucun véhicule ne peut être affecté.</p>
                    <table style="width:100%;border-collapse:collapse;margin:16px 0;">
                        <tr style="background:#fef2f2;">
                            <td style="padding:8px 12px;font-weight:bold;width:40%;">Référence mission</td>
                            <td style="padding:8px 12px;">{mission.Reference}</td>
                        </tr>
                        <tr>
                            <td style="padding:8px 12px;font-weight:bold;">Adresse</td>
                            <td style="padding:8px 12px;">{System.Net.WebUtility.HtmlEncode(mission.TargetAddress)}</td>
                        </tr>
                        <tr style="background:#fef2f2;">
                            <td style="padding:8px 12px;font-weight:bold;">Statut</td>
                            <td style="padding:8px 12px;">{mission.Status}</td>
                        </tr>
                        <tr>
                            <td style="padding:8px 12px;font-weight:bold;">Heure</td>
                            <td style="padding:8px 12px;">{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</td>
                        </tr>
                    </table>
                    <p style="color:#dc2626;font-weight:bold;">Une intervention manuelle est nécessaire.</p>
                    <hr style="border:none;border-top:1px solid #e2e8f0;margin:24px 0;"/>
                    <small style="color:#64748b;">PrediCop — Police Municipale | Ce message est généré automatiquement.</small>
                    </body></html>
                    """;
                await emailService.SendToManagersAsync(TenantId, subject, htmlBody, ct);
            }
            catch (Exception)
            {
                // L'envoi email ne doit jamais faire échouer l'action principale
            }

            // Refusal was committed — return 200 so the mobile dismisses the proposal correctly.
            // The manager email already notifies the dispatch issue.
            await db.Entry(assignment).ReloadAsync(ct);
            return Ok(MapAssignmentToResponse(assignment));
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
            await BroadcastMissionStatusToVehiclesAsync(response, ct);

            return Ok(response);
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors de la complétion", detail: ex.Message, statusCode: 500);
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Admin,Manager,Operator")]
    public async Task<ActionResult<MissionResponse>> Cancel(
        Guid id,
        [FromBody] CancelMissionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Problem(title: "Le motif d'annulation est requis", statusCode: 400);

        var mission = await db.Missions
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvÃ©e", statusCode: 404);

        try
        {
            var cancelled = await missionService.CancelMissionAsync(id, request.Reason.Trim(), ct);

            await db.Entry(cancelled).Reference(m => m.Call).LoadAsync(ct);
            await db.Entry(cancelled).Collection(m => m.Assignments).LoadAsync(ct);
            foreach (var a in cancelled.Assignments)
                await db.Entry(a).Reference(x => x.Vehicle).LoadAsync(ct);

            var response = MapToResponse(cancelled);

            await hubContext.Clients
                .Group($"operators_{TenantId}")
                .SendAsync("MissionStatusChanged", response, ct);
            await BroadcastMissionStatusToVehiclesAsync(response, ct);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Annulation impossible", detail: ex.Message, statusCode: 400);
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors de l'annulation", detail: ex.Message, statusCode: 500);
        }
    }

    // -------- Intervenants --------

    [HttpPost("{id:guid}/intervenants")]
    public async Task<ActionResult<MissionIntervenantResponse>> AddIntervenant(
        Guid id, [FromBody] CreateMissionIntervenantRequest request, CancellationToken ct)
    {
        var exists = await db.Missions.AnyAsync(m => m.Id == id && m.TenantId == TenantId, ct);
        if (!exists) return Problem(title: "Mission non trouvée", statusCode: 404);

        var order = await db.MissionIntervenants
            .Where(i => i.MissionId == id)
            .MaxAsync(i => (int?)i.Order, ct) ?? 0;

        var intervenant = new MissionIntervenant
        {
            TenantId = TenantId,
            MissionId = id,
            FullName = request.FullName,
            Role = request.Role,
            PhoneNumber = request.PhoneNumber,
            IsInjured = request.IsInjured,
            Notes = request.Notes,
            Order = order + 1
        };

        db.MissionIntervenants.Add(intervenant);
        await db.SaveChangesAsync(ct);
        return Ok(MapIntervenantToResponse(intervenant));
    }

    [HttpPut("{id:guid}/intervenants/{intervenantId:guid}")]
    public async Task<ActionResult<MissionIntervenantResponse>> UpdateIntervenant(
        Guid id, Guid intervenantId,
        [FromBody] UpdateMissionIntervenantRequest request,
        CancellationToken ct)
    {
        var intervenant = await db.MissionIntervenants
            .FirstOrDefaultAsync(i => i.Id == intervenantId && i.MissionId == id && i.TenantId == TenantId, ct);

        if (intervenant is null) return Problem(title: "Intervenant non trouvé", statusCode: 404);

        if (request.FullName is not null) intervenant.FullName = request.FullName;
        if (request.Role is not null) intervenant.Role = request.Role;
        if (request.PhoneNumber is not null) intervenant.PhoneNumber = request.PhoneNumber;
        if (request.IsInjured.HasValue) intervenant.IsInjured = request.IsInjured.Value;
        if (request.Notes is not null) intervenant.Notes = request.Notes;

        await db.SaveChangesAsync(ct);
        return Ok(MapIntervenantToResponse(intervenant));
    }

    [HttpDelete("{id:guid}/intervenants/{intervenantId:guid}")]
    public async Task<IActionResult> DeleteIntervenant(
        Guid id, Guid intervenantId, CancellationToken ct)
    {
        var intervenant = await db.MissionIntervenants
            .FirstOrDefaultAsync(i => i.Id == intervenantId && i.MissionId == id && i.TenantId == TenantId, ct);

        if (intervenant is null) return Problem(title: "Intervenant non trouvé", statusCode: 404);

        intervenant.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // -------- Force Assign --------

    [HttpPost("{id:guid}/force-assign")]
    [Authorize(Roles = "Admin,Manager,Operator")]
    public async Task<ActionResult<MissionResponse>> ForceAssign(
        Guid id,
        [FromBody] ForceAssignRequest request,
        CancellationToken ct)
    {
        // 1. Charger la mission
        var mission = await db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        // 2. Vérifier la priorité
        if (mission.Priority < CallPriority.Critique)
            return Problem(
                title: "Priorité insuffisante",
                detail: "Le force-assign n'est autorisé que pour les missions de priorité Critique ou SOS.",
                statusCode: 400);

        // 3. Charger le véhicule cible
        var vehicle = await db.PatrolVehicles
            .Include(v => v.Officers).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId && v.TenantId == TenantId, ct);

        if (vehicle is null)
            return Problem(title: "Véhicule non trouvé", statusCode: 404);

        // 4. Si le véhicule est déjà en mission, libérer son assignment actif
        if (vehicle.Status == VehicleStatus.OnMission)
        {
            var activeAssignment = await db.MissionAssignments
                .Include(a => a.Mission).ThenInclude(m => m.Assignments)
                .FirstOrDefaultAsync(a =>
                    a.VehicleId == vehicle.Id
                    && (a.Status == MissionStatus.Accepted || a.Status == MissionStatus.Proposed),
                    ct);

            if (activeAssignment is not null)
            {
                activeAssignment.Status = MissionStatus.Refused;
                activeAssignment.RefusalReasonCode = RefusalReasonCode.Other;
                activeAssignment.RefusalReason = "Réaffecté pour mission prioritaire";
                activeAssignment.RespondedAt = DateTime.UtcNow;

                // Remettre la mission précédente en Pending si plus d'assignment actif
                var prevMission = activeAssignment.Mission;
                var hasActiveAssignment = prevMission.Assignments
                    .Any(a => a.Id != activeAssignment.Id
                              && (a.Status == MissionStatus.Accepted || a.Status == MissionStatus.InProgress));

                if (!hasActiveAssignment && prevMission.Status == MissionStatus.InProgress)
                    prevMission.Status = MissionStatus.Pending;
            }
        }

        // 5. Libérer temporairement le véhicule
        vehicle.Status = VehicleStatus.Available;

        // 6. Créer l'assignment direct
        var nextOrder = mission.Assignments.Any()
            ? mission.Assignments.Max(a => a.ProposalOrder) + 1
            : 1;

        var newAssignment = new MissionAssignment
        {
            MissionId = mission.Id,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            ProposalOrder = nextOrder,
            Status = MissionStatus.Accepted,
            ProposedAt = DateTime.UtcNow,
            RespondedAt = DateTime.UtcNow,
            DistanceAtProposal = 0
        };
        db.MissionAssignments.Add(newAssignment);

        // 7. Mettre à jour la mission
        mission.Status = MissionStatus.InProgress;
        mission.AcceptedAt = DateTime.UtcNow;

        // 8. Mettre le véhicule en OnMission
        vehicle.Status = VehicleStatus.OnMission;

        await db.SaveChangesAsync(ct);

        // Reload pour le mapping
        await db.Entry(mission).Collection(m => m.Assignments).LoadAsync(ct);
        foreach (var a in mission.Assignments)
            await db.Entry(a).Reference(x => x.Vehicle).LoadAsync(ct);

        var missionResponse = MapToResponse(mission);

        // 9. Push notification à l'équipage
        var deviceTokens = vehicle.Officers
            .Where(o => o.IsActive && !string.IsNullOrWhiteSpace(o.User?.DeviceToken))
            .Select(o => o.User!.DeviceToken!)
            .ToList();

        if (deviceTokens.Any())
        {
            try
            {
                await pushService.SendToDevicesAsync(
                    deviceTokens,
                    "🚨 MISSION FORCÉE - Priorité absolue",
                    $"Mission {mission.Reference} — {mission.TargetAddress}",
                    new Dictionary<string, string>
                    {
                        ["missionId"] = mission.Id.ToString(),
                        ["type"] = "ForceAssign"
                    },
                    ct);
            }
            catch (Exception)
            {
                // Le push ne doit pas faire échouer l'action principale
            }
        }

        // 10. SignalR — opérateurs
        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("MissionStatusChanged", missionResponse, ct);

        // 11. SignalR — tablette véhicule
        await hubContext.Clients
            .Group($"vehicle_{vehicle.Id}")
            .SendAsync("MissionProposed", MapAssignmentToResponse(newAssignment), ct);

        return Ok(missionResponse);
    }

    // -------- Ajout d'un équipage en renfort --------

    [HttpPost("{id:guid}/crew")]
    [Authorize(Roles = "Admin,Manager,Operator")]
    public async Task<ActionResult<MissionAssignmentResponse>> AddCrew(
        Guid id,
        [FromBody] AddCrewRequest request,
        CancellationToken ct)
    {
        try
        {
            var assignment = await missionService.AddCrewToMissionAsync(id, request.VehicleId, TenantId, ct);

            await db.Entry(assignment).Reference(a => a.Vehicle).LoadAsync(ct);

            var assignmentResponse = MapAssignmentToResponse(assignment);

            // Notifier les opérateurs
            var mission = await db.Missions
                .Include(m => m.Call)
                .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
                .FirstOrDefaultAsync(m => m.Id == id, ct);

            if (mission is not null)
            {
                await hubContext.Clients
                    .Group($"operators_{TenantId}")
                    .SendAsync("MissionStatusChanged", MapToResponse(mission), ct);
            }

            // Notifier le véhicule ajouté
            await hubContext.Clients
                .Group($"vehicle_{request.VehicleId}")
                .SendAsync("MissionProposed", assignmentResponse, ct);

            return Ok(assignmentResponse);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Impossible d'ajouter l'équipage", detail: ex.Message, statusCode: 400);
        }
        catch (Exception ex)
        {
            return Problem(title: "Erreur lors de l'ajout de l'équipage", detail: ex.Message, statusCode: 500);
        }
    }

    // -------- Mappers --------

    private static MissionResponse MapToResponse(Mission m) => new()
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
        DispatchedAt = m.DispatchedAt,
        AcceptedAt = m.AcceptedAt,
        ArrivedAt = m.ArrivedAt,
        CompletedAt = m.Status == MissionStatus.Cancelled ? null : m.CompletedAt,
        CancelledAt = m.Status == MissionStatus.Cancelled ? m.CompletedAt : null,
        CompletionReport = m.CompletionReport,
        CancellationReason = m.Status == MissionStatus.Cancelled ? m.CompletionReport : null,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        AssignedVehicleCallSign = m.Assignments
            .Where(a => a.Status == MissionStatus.Accepted
                     || a.Status == MissionStatus.InProgress
                     || a.Status == MissionStatus.Proposed)
            .OrderByDescending(a => a.ProposedAt)
            .Select(a => a.Vehicle?.CallSign)
            .FirstOrDefault(cs => !string.IsNullOrEmpty(cs)),
        Assignments = m.Assignments.Select(MapAssignmentToResponse).ToList(),
        Intervenants = m.Intervenants.OrderBy(i => i.Order).Select(MapIntervenantToResponse).ToList(),
        Media = m.MediaAttachments.OrderByDescending(ma => ma.RecordedAt).Select(MapMediaToResponse).ToList(),
        CallerName = m.Call?.CallerName,
        CallerPhone = m.Call?.CallerPhone,
        IncidentCategory = m.Call?.IncidentCategory,
        IncidentAddressComplement = m.Call?.IncidentAddressComplement,
        CallNotes = m.Call?.Notes,
        ThirdParties = m.Call?.ThirdParties,
    };

    private static MissionIntervenantResponse MapIntervenantToResponse(MissionIntervenant i) => new()
    {
        Id = i.Id,
        FullName = i.FullName,
        Role = i.Role,
        PhoneNumber = i.PhoneNumber,
        IsInjured = i.IsInjured,
        Notes = i.Notes,
        Order = i.Order
    };

    private static MediaAttachmentResponse MapMediaToResponse(MediaAttachment ma) => new()
    {
        Id = ma.Id,
        MissionId = ma.MissionId,
        FileName = ma.FileName,
        ContentType = ma.ContentType,
        FileSizeBytes = ma.FileSizeBytes,
        DurationSeconds = ma.DurationSeconds,
        RecordedAt = ma.RecordedAt,
        CameraDeviceId = ma.CameraDeviceId,
        CreatedByUserId = ma.CreatedByUserId,
        CreatedByName = ma.CreatedBy?.FullName ?? string.Empty,
        CreatedAt = ma.CreatedAt
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
        RefusalReasonCode = a.RefusalReasonCode,
        RefusalReason = a.RefusalReason,
        DistanceAtProposal = a.DistanceAtProposal
    };

    /// <summary>
    /// Recharge la mission et pousse <c>MissionStatusChanged</c> au groupe des opérateurs du tenant
    /// pour que la liste des missions / équipages du Back Office se rafraîchisse en temps réel.
    /// </summary>
    private async Task NotifyOperatorsOfProposalAsync(Guid missionId, CancellationToken ct)
    {
        var mission = await db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .FirstOrDefaultAsync(m => m.Id == missionId && m.TenantId == TenantId, ct);

        if (mission is null) return;

        await hubContext.Clients
            .Group($"operators_{TenantId}")
            .SendAsync("MissionStatusChanged", MapToResponse(mission), ct);
        logger.LogInformation("[Propose] MissionStatusChanged → groupe operators_{TenantId} pour mission {MissionId}",
            TenantId, missionId);
        flowLog.Log("Server", "Information", "Dispatch",
            $"MissionStatusChanged → operators_{TenantId} (rafraîchissement BO)", TenantId, CurrentUserId, $"missionId={missionId}");
    }

    private Task BroadcastMissionStatusToVehiclesAsync(MissionResponse mission, CancellationToken ct)
    {
        var vehicleIds = mission.Assignments
            .Select(a => a.VehicleId)
            .Distinct()
            .ToList();

        return Task.WhenAll(vehicleIds.Select(vehicleId =>
            hubContext.Clients
                .Group($"vehicle_{vehicleId}")
                .SendAsync("MissionStatusChanged", mission, ct)));
    }
}
