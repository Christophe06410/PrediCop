namespace PrediCop.Core.Entities;

/// <summary>
/// Journal des erreurs graves remontées par l'application mobile.
/// TenantId et UserId sont nullable : une erreur peut survenir avant la connexion.
/// N'hérite pas de BaseEntity pour éviter le filtre global IsDeleted.
/// </summary>
public class MobileErrorLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Nullable — peut être null si l'erreur se produit avant le login.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Nullable — peut être null si l'erreur se produit avant le login.</summary>
    public Guid? UserId { get; set; }

    public string AppVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;   // "Android", "iOS", "Windows"
    public string DeviceModel { get; set; } = string.Empty;

    /// <summary>Catégorie courte : "ApiError", "UnhandledException", "NetworkError", etc.</summary>
    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Stack trace ou détails techniques — peut être null pour les erreurs réseau simples.</summary>
    public string? StackTrace { get; set; }

    /// <summary>Endpoint HTTP concerné le cas échéant (ex: "POST api/missions").</summary>
    public string? Endpoint { get; set; }

    /// <summary>Code HTTP reçu le cas échéant.</summary>
    public int? HttpStatusCode { get; set; }
}
