using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Infrastructure.Services;

public class MissionService(AppDbContext context, IGpsService gpsService) : IMissionService
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
            BriefingText = call.IncidentDescription
        };

        context.Missions.Add(mission);
        call.Status = CallStatus.MissionCreated;
        await context.SaveChangesAsync(ct);

        // Auto-dispatch; if no vehicle available yet, mission stays Pending for manual dispatch
        try { await ProposeToNextVehicleAsync(mission.Id, ct); }
        catch (InvalidOperationException) { }

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

        var nearby = await gpsService.FindNearbyAvailableVehiclesAsync(
            mission.TargetLatitude, mission.TargetLongitude, 5, ct);

        var next = nearby.FirstOrDefault(v => !alreadyProposedVehicleIds.Contains(v.VehicleId));

        if (next == default)
            throw new InvalidOperationException("No available vehicle found for this mission.");

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

        mission.Status = MissionStatus.Proposed;

        await context.SaveChangesAsync(ct);

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
            assignment.Mission.Status = MissionStatus.Accepted;
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

        var activeAssignment = mission.Assignments
            .FirstOrDefault(a => a.Status == MissionStatus.Accepted);

        if (activeAssignment is not null)
        {
            var vehicle = await context.PatrolVehicles.FindAsync([activeAssignment.VehicleId], ct);
            if (vehicle is not null)
                vehicle.Status = VehicleStatus.Available;
        }

        var call = await context.Calls.FindAsync([mission.CallId], ct);
        if (call is not null)
            call.Status = CallStatus.Closed;

        await context.SaveChangesAsync(ct);
        return mission;
    }
}
