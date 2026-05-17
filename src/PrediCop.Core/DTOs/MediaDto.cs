namespace PrediCop.Core.DTOs;

public class MediaAttachmentResponse
{
    public Guid Id { get; set; }
    public Guid? MissionId { get; set; }
    public string? MissionReference { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentReference { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime RecordedAt { get; set; }
    public string? CameraDeviceId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
