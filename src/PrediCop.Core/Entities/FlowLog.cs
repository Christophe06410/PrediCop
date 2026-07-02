namespace PrediCop.Core.Entities;

/// <summary>
/// Journal technique persistant (serveur + mobile) destiné au diagnostic des flux
/// temps réel (dispatch, SignalR…). Volontairement HORS multi-tenant (pas de filtre global)
/// pour rester inscriptible depuis n'importe quel contexte, y compris le Hub SignalR.
/// </summary>
public class FlowLog
{
    public long Id { get; set; }

    /// <summary>Horodatage UTC.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>"Server" ou "Mobile".</summary>
    public string Source { get; set; } = "Server";

    /// <summary>Information / Warning / Error…</summary>
    public string Level { get; set; } = "Information";

    /// <summary>Catégorie ou tag (ex. "Dispatch", "SignalR", "Hub").</summary>
    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }

    /// <summary>Contexte optionnel (vehicleId, connectionId, missionId…).</summary>
    public string? Context { get; set; }
}
