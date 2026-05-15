namespace PrediCop.BackOffice.Models;

public class MissionDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid CallId { get; set; }
    public string CallReference { get; set; } = string.Empty;
    public string TargetAddress { get; set; } = string.Empty;
    public double TargetLatitude { get; set; }
    public double TargetLongitude { get; set; }
    public string BriefingText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionReport { get; set; }
    public string? AssignedVehicleCallSign { get; set; }
    public List<MissionAssignmentDto> Assignments { get; set; } = [];
}

public class MissionAssignmentDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleCallSign { get; set; } = string.Empty;
    public int ProposalOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ProposedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RefusalReason { get; set; }
    public double DistanceAtProposal { get; set; }
}
