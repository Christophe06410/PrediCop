namespace PrediCop.Core.DTOs;

public class SearchResultItem
{
    public string Type { get; set; } = string.Empty;       // "Appel", "Mission", "Document", "Entrée"
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;      // texte principal affiché
    public string Subtitle { get; set; } = string.Empty;   // texte secondaire (adresse, statut, etc.)
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ParentId { get; set; }                  // ex: MissionId pour un Document
}

public class SearchResponse
{
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<SearchResultItem> Results { get; set; } = [];
}
