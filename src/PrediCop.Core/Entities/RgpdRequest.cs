using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class RgpdRequest : TenantEntity
{
    public RgpdRequestType RequestType { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; } = false;
    public DateTime? ProcessedAt { get; set; }
    public string? AdminNotes { get; set; }
}
