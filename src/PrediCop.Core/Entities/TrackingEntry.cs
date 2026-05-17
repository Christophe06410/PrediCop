using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class TrackingEntry : TenantEntity
{
    public Guid DocumentId { get; set; }
    public TrackingDocument Document { get; set; } = null!;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public EntryType Type { get; set; }
    public string Content { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;
}
