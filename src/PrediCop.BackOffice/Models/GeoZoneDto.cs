namespace PrediCop.BackOffice.Models;

public class GeoZoneDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public bool IsActive { get; set; }
}
