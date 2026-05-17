namespace PrediCop.BackOffice.Models;

public class SearchResultItemDto
{
    public string Type { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ParentId { get; set; }
}

public class SearchResponseDto
{
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<SearchResultItemDto> Results { get; set; } = [];
}
