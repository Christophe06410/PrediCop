using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class TrackingDocument : TenantEntity
{
    public string Reference { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Brouillon;
    public string Title { get; set; } = string.Empty;

    public Guid MissionId { get; set; }
    public Mission Mission { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    public ICollection<TrackingEntry> Entries { get; set; } = [];
}
