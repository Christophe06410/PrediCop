using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class Mission : TenantEntity
{
    public string Reference { get; set; } = string.Empty;
    public MissionStatus Status { get; set; } = MissionStatus.Pending;
    public CallPriority Priority { get; set; } = CallPriority.Routine;

    public Guid CallId { get; set; }
    public Call Call { get; set; } = null!;

    public string TargetAddress { get; set; } = string.Empty;
    public double TargetLatitude { get; set; }
    public double TargetLongitude { get; set; }

    public string BriefingText { get; set; } = string.Empty;
    public string? LocationDetail { get; set; }
    public string? NarrativeReport { get; set; }

    public DateTime? DispatchedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionReport { get; set; }

    public ICollection<MissionAssignment> Assignments { get; set; } = [];
    public ICollection<MissionIntervenant> Intervenants { get; set; } = [];
    public ICollection<MediaAttachment> MediaAttachments { get; set; } = [];
}
