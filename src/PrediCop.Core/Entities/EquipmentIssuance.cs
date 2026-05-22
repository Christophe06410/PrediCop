namespace PrediCop.Core.Entities;

public class EquipmentIssuance : TenantEntity
{
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public Guid EquipmentCatalogId { get; set; }
    public EquipmentCatalog Equipment { get; set; } = null!;

    public int Quantity { get; set; } = 1;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string? Size { get; set; }
    public string? SerialNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsReturned { get; set; } = false;
    public DateTime? ReturnedAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt < DateTime.UtcNow;
    public bool ExpiresWithin60Days => !IsExpired && ExpiresAt.HasValue && ExpiresAt < DateTime.UtcNow.AddDays(60);
}
