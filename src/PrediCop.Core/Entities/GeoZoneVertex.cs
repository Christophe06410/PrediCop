namespace PrediCop.Core.Entities;

public class GeoZoneVertex : BaseEntity
{
    public Guid GeoZoneId { get; set; }
    public GeoZone GeoZone { get; set; } = null!;
    public int Order { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
