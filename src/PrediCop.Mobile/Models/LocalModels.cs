using SQLite;

namespace PrediCop.Mobile.Models;

[Table("CachedMission")]
public class CachedMission
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string Reference { get; set; } = "";
    public string TargetAddress { get; set; } = "";
    public string Status { get; set; } = "";
    public string? BriefingText { get; set; }
    public DateTime CachedAt { get; set; }

    /// <summary>JSON complet de la mission pour affichage riche hors-ligne.</summary>
    public string RawJson { get; set; } = "";
}

[Table("PendingTrackingEntry")]
public class PendingTrackingEntry
{
    [PrimaryKey, AutoIncrement]
    public int LocalId { get; set; }

    public Guid MissionId { get; set; }

    /// <summary>"note", "gps" ou "status"</summary>
    public string EntryType { get; set; } = "";

    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsSynced { get; set; }
}
