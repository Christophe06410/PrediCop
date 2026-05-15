namespace PrediCop.Core.DTOs;

public class StreetResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? District { get; set; }
    public string? City { get; set; }
    public double StartLatitude { get; set; }
    public double StartLongitude { get; set; }
    public double EndLatitude { get; set; }
    public double EndLongitude { get; set; }
    public string? GeoJson { get; set; }
    public int BaseRiskScore { get; set; }
    public int CurrentRiskScore { get; set; }
    public DateTime? LastPatrolledAt { get; set; }
    public int PatrolIntervalHours { get; set; }
    public bool IsOverdue => LastPatrolledAt == null
        || (DateTime.UtcNow - LastPatrolledAt.Value).TotalHours > PatrolIntervalHours;
}

public class PatrolRequest
{
    public Guid VehicleId { get; set; }
}

public class RiskEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RiskPoints { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class UpdateBaseRiskRequest
{
    public int BaseRiskScore { get; set; }
}
