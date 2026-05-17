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
    IEmailService emailService) : ControllerBase
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
            .Include(m => m.Intervenants.OrderBy(i => i.Order))
            .Include(m => m.MediaAttachments).ThenInclude(ma => ma.CreatedBy)
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == TenantId, ct);

        if (mission is null)
            return Problem(title: "Mission non trouvée", statusCode: 404);

        return Ok(MapToResponse(mission));
    }

    [HttpGet("active")]
    public async Task<ActionResult<List<MissionResponse>>> GetActiveMissions(CancellationToken ct)
    {
        var query = db.Missions
            .Include(m => m.Call)
            .Include(m => m.Assignments).ThenInclude(a => a.Vehicle)
            .Where(m => m.TenantId == TenantId
                && (m.Status == MissionStatus.Pending
                    || m.Status == MissionStatus.Proposed
                    || m.Status == MissionStatus.Accepted
                    || m.Status == MissionStatus.InProgress));

        // When the JWT carries a vehicleId (mobile officer), restrict to missions
        // where that specific vehicle has a relevant assignment.
        var vehicleIdClaim = User.FindFirst("vehicleId")?.Value;
        if (Guid.TryParse(vehicleIdClaim, out var vehicleId))
        {
            query = query.Where(m => m.Assignments.Any(a =>
                a.VehicleId == vehicleId &&
                (a.Status == MissionStatus.Proposed ||
                 a.Status == MissionStatus.Accepted ||
                 a.Status == MissionStatus.InProgress)));
        }

        var missions = await query
            .OrderBy(m => m.CreatedAt)
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
        catch (InvalidOperationException ex) when (ex.Message.Contains("No available vehicle"))
        {
            // Dispatch épuisé — plus aucun véhicule disponible pour cette mission
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

            return Problem(title: "Dispatch épuisé — aucun véhicule disponible", detail: ex.Message, statusCode: 503);
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

    // -------- Mappers --------

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
        LocationDetail = m.LocationDetail,
        NarrativeReport = m.NarrativeReport,
        DispatchedAt = m.DispatchedAt,
        AcceptedAt = m.AcceptedAt,
        ArrivedAt = m.ArrivedAt,
        CompletedAt = m.CompletedAt,
        CompletionReport = m.CompletionReport,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Assignments = m.Assignments.Select(MapAssignmentToResponse).ToList(),
        Intervenants = m.Intervenants.OrderBy(i => i.Order).Select(MapIntervenantToResponse).ToList(),
        Media = m.MediaAttachments.OrderByDescending(ma => ma.RecordedAt).Select(MapMediaToResponse).ToList()
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
}
