namespace PrediCop.Core.DTOs;

public class GeoZoneVertexDto
{
    public int Order { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class GeoZoneResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public bool IsActive { get; set; }
    public List<GeoZoneVertexDto> Vertices { get; set; } = [];
}

public class CreateGeoZoneRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public List<GeoZoneVertexDto> Vertices { get; set; } = [];
}

public class UpdateGeoZoneRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool? IsActive { get; set; }
    public List<GeoZoneVertexDto>? Vertices { get; set; }
}
