namespace PrediCop.BackOffice.Models;

public class MediaAttachmentDto
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
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string FileSizeLabel => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} o",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} Ko",
        _ => $"{FileSizeBytes / 1024.0 / 1024.0:F1} Mo"
    };

    public bool IsVideo => ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
}
