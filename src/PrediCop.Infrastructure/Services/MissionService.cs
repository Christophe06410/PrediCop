using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Infrastructure.Services;

public class MissionService(
    AppDbContext context,
    IGpsService gpsService,
    INotificationCoordinator notificationCoordinator,
    ILogger<MissionService> logger) : IMissionService
{
    public async Task<Mission> CreateMissionFromCallAsync(Guid callId, CancellationToken ct = default)
    {
        var call = await context.Calls.FindAsync([callId], ct)
            ?? throw new InvalidOperationException($"Call {callId} not found.");

        var reference = $"MSN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

        var mission = new Mission
        {
            TenantId = call.TenantId,
            CallId = callId,
            Reference = reference,
            Status = MissionStatus.Pending,
            TargetAddress = call.IncidentAddress,
            TargetLatitude = call.IncidentLatitude ?? 0,
            TargetLongitude = call.IncidentLongitude ?? 0,
            BriefingText = call.IncidentDescription,
            Priority = call.Priority,
        };

        context.Missions.Add(mission);
        // Si l'appel était fermé (reprise), le rouvrir ; sinon marquer MissionCreated
        call.Status = call.Status == CallStatus.Closed
            ? CallStatus.InProgress
            : CallStatus.MissionCreated;
        await context.SaveChangesAsync(ct);

        // Auto-dispatch: si aucun véhicule disponible, la mission reste Pending pour dispatch manuel
        try { await ProposeToNextVehicleAsync(mission.Id, ct); }
        catch (Exception ex) { logger.LogInformation("Auto-dispatch skipped for {MissionId}: {Reason}", mission.Id, ex.Message); }

        return mission;
    }

    public async Task<MissionAssignment> ProposeToNextVehicleAsync(Guid missionId, CancellationToken ct = default)
    {
        var mission = await context.Missions
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct)
            ?? throw new InvalidOperationException($"Mission {missionId} not found.");

        var alreadyProposedVehicleIds = mission.Assignments
            .Select(a => a.VehicleId)
            .ToHashSet();

        var nearby = (await gpsService.FindNearbyAvailableVehiclesAsync(
            mission.TargetLatitude, mission.TargetLongitude, mission.TenantId, 5, ct)).ToList();

        // Un véhicule n'est "bloqué" que s'il a une assignation active (Proposed/Accepted/InProgress)
        // sur une mission ELLE-MÊME encore active. Sans le filtre sur le statut de la mission parente,
        // toute assignation Accepted/InProgress laissée par une mission terminée bloquait le véhicule
        // à vie (CompleteMissionAsync ne repassait pas l'assignation en Completed).
        var blockedVehicleIds = await context.MissionAssignments
            .Where(a => a.MissionId != missionId
                && (a.Status == MissionStatus.Proposed
                    || a.Status == MissionStatus.Accepted
                    || a.Status == MissionStatus.InProgress)
                && a.Mission.Status != MissionStatus.Completed
                && a.Mission.Status != MissionStatus.Cancelled)
            .Select(a => a.VehicleId)
            .Distinct()
            .ToListAsync(ct);

        logger.LogInformation(
            "Dispatch {MissionId}: {NearbyCount} Available vehicle(s) for tenant, {BlockedCount} blocked on other missions, {AlreadyCount} already proposed to this mission",
            missionId, nearby.Count, blockedVehicleIds.Count, alreadyProposedVehicleIds.Count);

        var next = nearby.FirstOrDefault(v =>
            !alreadyProposedVehicleIds.Contains(v.VehicleId)
            && !blockedVehicleIds.Contains(v.VehicleId));

        if (next == default)
        {
            logger.LogWarning(
                "Dispatch {MissionId}: no candidate vehicle — nearby={Near}, blocked={Blocked}, already={Already}",
                missionId, nearby.Count, blockedVehicleIds.Count, alreadyProposedVehicleIds.Count);
            throw new InvalidOperationException("No available vehicle found for this mission.");
        }

        var order = mission.Assignments.Count + 1;

        var assignment = new MissionAssignment
        {
            MissionId = missionId,
            VehicleId = next.VehicleId,
            ProposalOrder = order,
            Status = MissionStatus.Proposed,
            ProposedAt = DateTime.UtcNow,
            DistanceAtProposal = next.Distance
        };

        context.MissionAssignments.Add(assignment);

        // La mission reste Pending jusqu'à acceptation

        await context.SaveChangesAsync(ct);

        logger.LogInformation("Dispatch {MissionId}: proposed to vehicle {VehicleId} (order={Order}, distance={Dist:F1}km)",
            missionId, next.VehicleId, order, next.Distance);

        var title = mission.Priority >= CallPriority.Critique
            ? $"🚨 {mission.Priority.ToString().ToUpper()} — Mission {mission.Reference}"
            : $"Nouvelle mission {mission.Reference}";
        await notificationCoordinator.NotifyMissionProposedAsync(
            assignment.Id, assignment.VehicleId,
            title, $"Mission {mission.Reference} — {mission.TargetAddress}", ct);

        return assignment;
    }

    public async Task<MissionAssignment> RespondToProposalAsync(Guid assignmentId, bool accepted, RefusalReasonCode? reasonCode, string? refusalReason, CancellationToken ct = default)
    {
        var assignment = await context.MissionAssignments
            .Include(a => a.Mission)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new InvalidOperationException($"Assignment {assignmentId} not found.");

        assignment.RespondedAt = DateTime.UtcNow;

        if (accepted)
        {
            assignment.Status = MissionStatus.Accepted;
            assignment.Mission.Status = MissionStatus.InProgress;
            assignment.Mission.AcceptedAt = DateTime.UtcNow;

            var vehicle = await context.PatrolVehicles.FindAsync([assignment.VehicleId], ct);
            if (vehicle is not null)
                vehicle.Status = VehicleStatus.OnMission;
        }
        else
        {
            assignment.Status = MissionStatus.Refused;
            assignment.RefusalReasonCode = reasonCode;
            assignment.RefusalReason = refusalReason;

            await context.SaveChangesAsync(ct);
            await ProposeToNextVehicleAsync(assignment.MissionId, ct);
            return assignment;
        }

        await context.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<Mission> CompleteMissionAsync(Guid missionId, string report, CancellationToken ct = default)
    {
        var mission = await context.Missions
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct)
            ?? throw new InvalidOperationException($"Mission {missionId} not found.");

        mission.Status = MissionStatus.Completed;
        mission.CompletedAt = DateTime.UtcNow;
        mission.CompletionReport = report;

        var activeVehicleIds = mission.Assignments
            .Where(a => a.Status == MissionStatus.Accepted || a.Status == MissionStatus.InProgress)
            .Select(a => a.VehicleId)
            .Distinct()
            .ToList();

        // Clôturer les assignations actives : sans ça elles restaient en Accepted et
        // bloquaient le véhicule pour tout dispatch futur (voir blockedVehicleIds).
        foreach (var assignment in mission.Assignments.Where(a =>
                     a.Status == MissionStatus.Accepted || a.Status == MissionStatus.InProgress))
        {
            assignment.Status = MissionStatus.Completed;
            assignment.RespondedAt ??= DateTime.UtcNow;
        }

        foreach (var vehicleId in activeVehicleIds)
        {
            var vehicle = await context.PatrolVehicles.FindAsync([vehicleId], ct);
            if (vehicle is not null)
                vehicle.Status = VehicleStatus.Available;
        }

        var call = await context.Calls.FindAsync([mission.CallId], ct);
        if (call is not null)
            call.Status = CallStatus.Closed;

        await context.SaveChangesAsync(ct);
        return mission;
    }

    public async Task<Mission> CancelMissionAsync(Guid missionId, string reason, CancellationToken ct = default)
    {
        var mission = await context.Missions
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == missionId, ct)
            ?? throw new InvalidOperationException($"Mission {missionId} not found.");

        if (mission.Status == MissionStatus.Completed)
            throw new InvalidOperationException("Une mission terminée ne peut pas être annulée.");

        if (mission.Status == MissionStatus.Cancelled)
            throw new InvalidOperationException("La mission est déjà annulée.");

        mission.Status = MissionStatus.Cancelled;
        mission.CompletedAt = DateTime.UtcNow;
        mission.CompletionReport = reason;

        var impactedVehicleIds = mission.Assignments
            .Where(a => a.Status == MissionStatus.Proposed
                || a.Status == MissionStatus.Accepted
                || a.Status == MissionStatus.InProgress)
            .Select(a => a.VehicleId)
            .Distinct()
            .ToList();

        foreach (var assignment in mission.Assignments.Where(a =>
                     a.Status == MissionStatus.Proposed
                     || a.Status == MissionStatus.Accepted
                     || a.Status == MissionStatus.InProgress))
        {
            assignment.Status = MissionStatus.Cancelled;
            assignment.RespondedAt ??= DateTime.UtcNow;
            assignment.RefusalReason ??= reason;
        }

        foreach (var vehicleId in impactedVehicleIds)
        {
            var vehicle = await context.PatrolVehicles.FindAsync([vehicleId], ct);
            if (vehicle is not null)
                vehicle.Status = VehicleStatus.Available;
        }

        var call = await context.Calls.FindAsync([mission.CallId], ct);
        if (call is not null)
            call.Status = CallStatus.Closed;

        await context.SaveChangesAsync(ct);
        return mission;
    }

    public async Task<MissionAssignment> AddCrewToMissionAsync(Guid missionId, Guid vehicleId, Guid tenantId, CancellationToken ct = default)
    {
        var mission = await context.Missions
            .Include(m => m.Assignments)
            .FirstOrDefaultAsync(m => m.Id == missionId && m.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Mission {missionId} not found.");

        if (mission.Status != MissionStatus.InProgress && mission.Status != MissionStatus.Pending)
            throw new InvalidOperationException("Un équipage ne peut être ajouté qu'à une mission en attente ou en cours.");

        var vehicle = await context.PatrolVehicles
            .Include(v => v.Officers).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(v => v.Id == vehicleId && v.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Vehicle {vehicleId} not found.");

        if (vehicle.Status == VehicleStatus.OnMission)
            throw new InvalidOperationException("Ce véhicule est déjà en mission.");

        var alreadyAssigned = mission.Assignments
            .Any(a => a.VehicleId == vehicleId
                && (a.Status == MissionStatus.Proposed || a.Status == MissionStatus.Accepted));
        if (alreadyAssigned)
            throw new InvalidOperationException("Ce véhicule est déjà assigné à cette mission.");

        var order = mission.Assignments.Any()
            ? mission.Assignments.Max(a => a.ProposalOrder) + 1
            : 1;

        var assignment = new MissionAssignment
        {
            MissionId = missionId,
            VehicleId = vehicleId,
            ProposalOrder = order,
            Status = MissionStatus.Proposed,
            ProposedAt = DateTime.UtcNow,
            DistanceAtProposal = 0
        };

        context.MissionAssignments.Add(assignment);
        vehicle.Status = VehicleStatus.OnMission;
        await context.SaveChangesAsync(ct);

        var reinforcementTitle = mission.Priority >= CallPriority.Critique
            ? $"🚨 {mission.Priority.ToString().ToUpper()} — Renfort mission {mission.Reference}"
            : $"Renfort mission {mission.Reference}";
        await notificationCoordinator.NotifyMissionProposedAsync(
            assignment.Id, vehicleId,
            reinforcementTitle, $"Vous êtes ajouté en renfort — {mission.TargetAddress}", ct);

        return assignment;
    }
}
