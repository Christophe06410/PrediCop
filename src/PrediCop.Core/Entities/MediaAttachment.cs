namespace PrediCop.Core.Entities;

public class MediaAttachment : TenantEntity
{
    public Guid? MissionId { get; set; }
    public Mission? Mission { get; set; }

    public Guid? DocumentId { get; set; }
    public TrackingDocument? Document { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? CameraDeviceId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedBy { get; set; } = null!;

    // Relative path from the configured uploads base directory
    public string StoragePath { get; set; } = string.Empty;
}
