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
    public string? LocationDetail { get; set; }
    public string? NarrativeReport { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionReport { get; set; }
    public string? AssignedVehicleCallSign { get; set; }
    public List<MissionAssignmentDto> Assignments { get; set; } = [];
    public List<MissionIntervenantDto> Intervenants { get; set; } = [];
    public List<MissionMediaDto> Media { get; set; } = [];
}

public class MissionIntervenantDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsInjured { get; set; }
    public string? Notes { get; set; }
    public int Order { get; set; }
}

public class MissionMediaDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime RecordedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public bool IsImage => ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    public bool IsVideo => ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    public string SizeLabel => FileSizeBytes switch
    {
        >= 1_048_576 => $"{FileSizeBytes / 1_048_576.0:F1} Mo",
        >= 1_024 => $"{FileSizeBytes / 1_024.0:F0} Ko",
        _ => $"{FileSizeBytes} o"
    };
}

public class EditMissionDto
{
    public string TargetAddress { get; set; } = string.Empty;
    public string BriefingText { get; set; } = string.Empty;
    public string? CompletionReport { get; set; }
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
