using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class AgentQualification : TenantEntity
{
    public Guid AgentId { get; set; }
    public User Agent { get; set; } = null!;

    public QualificationType Type { get; set; }
    public string Reference { get; set; } = string.Empty;         // numéro de carte/certificat
    public string IssuingAuthority { get; set; } = string.Empty;  // autorité émettrice
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Notes { get; set; }

    // Calculé à l'affichage (pas en DB)
    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
    public bool ExpiresWithin30Days => !IsExpired && ExpiresAt < DateTime.UtcNow.AddDays(30);
}
