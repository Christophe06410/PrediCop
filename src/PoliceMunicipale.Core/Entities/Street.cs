namespace PoliceMunicipale.Core.Entities;

public class Street : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? City { get; set; }

    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double EndLatitude { get; set; }
    public double EndLongitude { get; set; }
    public string? GeoJson { get; set; }

    public int BaseRiskScore { get; set; } = 0;
    public int CurrentRiskScore { get; set; } = 0;
    public DateTime? LastPatrolledAt { get; set; }
    public int PatrolIntervalHours { get; set; } = 24;

    public ICollection<PatrolRecord> PatrolRecords { get; set; } = [];
    public ICollection<StreetRiskEvent> RiskEvents { get; set; } = [];
}
