namespace PoliceMunicipale.Core.Entities;

public class StreetRiskEvent : TenantEntity
{
    public Guid StreetId { get; set; }
    public Street Street { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RiskPoints { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Source { get; set; } = string.Empty;
}
