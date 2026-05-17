using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public class TrackingDocumentResponse
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid MissionId { get; set; }
    public string MissionReference { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TrackingEntryResponse> Entries { get; set; } = [];
}

public class TrackingEntryResponse
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public DateTime OccurredAt { get; set; }
    public EntryType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTrackingDocumentRequest
{
    public Guid MissionId { get; set; }
    public DocumentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class UpdateTrackingDocumentRequest
{
    public string? Title { get; set; }
    public DocumentStatus? Status { get; set; }
}

public class AddTrackingEntryRequest
{
    public EntryType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

public class UpdateTrackingEntryRequest
{
    public EntryType? Type { get; set; }
    public string? Content { get; set; }
    public DateTime? OccurredAt { get; set; }
}
