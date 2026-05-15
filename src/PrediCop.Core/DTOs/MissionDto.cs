using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public class CreateMissionRequest
{
    public Guid CallId { get; set; }
    public string TargetAddress { get; set; } = string.Empty;
    public double TargetLatitude { get; set; }
    public double TargetLongitude { get; set; }
    public string BriefingText { get; set; } = string.Empty;
}

public class CompleteMissionRequest
{
    public string Report { get; set; } = string.Empty;
}

public class RefuseMissionRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class MissionResponse
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public MissionStatus Status { get; set; }
    public Guid CallId { get; set; }
    public string CallReference { get; set; } = string.Empty;
    public string TargetAddress { get; set; } = string.Empty;
    public double TargetLatitude { get; set; }
    public double TargetLongitude { get; set; }
    public string BriefingText { get; set; } = string.Empty;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionReport { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<MissionAssignmentResponse> Assignments { get; set; } = [];
}

public class MissionAssignmentResponse
{
    public Guid Id { get; set; }
    public Guid MissionId { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleCallSign { get; set; } = string.Empty;
    public int ProposalOrder { get; set; }
    public MissionStatus Status { get; set; }
    public DateTime ProposedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? RefusalReason { get; set; }
    public double DistanceAtProposal { get; set; }
}
