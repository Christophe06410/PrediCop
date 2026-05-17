using PrediCop.Core.Entities;
using PrediCop.Core.Enums;

namespace PrediCop.Core.Interfaces;

public interface IMissionService
{
    Task<Mission> CreateMissionFromCallAsync(Guid callId, CancellationToken ct = default);
    Task<MissionAssignment> ProposeToNextVehicleAsync(Guid missionId, CancellationToken ct = default);
    Task<MissionAssignment> RespondToProposalAsync(Guid assignmentId, bool accepted, RefusalReasonCode? reasonCode, string? refusalReason, CancellationToken ct = default);
    Task<Mission> CompleteMissionAsync(Guid missionId, string report, CancellationToken ct = default);
}
