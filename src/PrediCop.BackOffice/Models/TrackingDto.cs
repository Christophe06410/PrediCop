using System.ComponentModel.DataAnnotations;

namespace PrediCop.BackOffice.Models;

public class TrackingDocumentDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid MissionId { get; set; }
    public string MissionReference { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TrackingEntryDto> Entries { get; set; } = [];
}

public class TrackingEntryDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTrackingDocumentDto
{
    public Guid MissionId { get; set; }

    [Required(ErrorMessage = "Le type est obligatoire.")]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le titre est obligatoire.")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;
}

public class AddTrackingEntryDto
{
    [Required(ErrorMessage = "Le type est obligatoire.")]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le contenu est obligatoire.")]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.Now;
}

public class UpdateTrackingStatusDto
{
    public string Status { get; set; } = string.Empty;
}
