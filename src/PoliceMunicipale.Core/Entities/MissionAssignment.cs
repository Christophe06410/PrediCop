using PoliceMunicipale.Core.Enums;

namespace PoliceMunicipale.Core.Entities;

public class MissionAssignment : BaseEntity
{
    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    public Guid VehicleId { get; set; }
    public PatrolVehicle Vehicle { get; set; } = null!;

    public int ProposalOrder { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.Proposed;
    public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? RefusalReason { get; set; }
    public double DistanceAtProposal { get; set; }
}
