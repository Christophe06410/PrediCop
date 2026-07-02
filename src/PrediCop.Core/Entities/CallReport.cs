using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class CallReport : TenantEntity
{
    public Guid CallId { get; set; }
    public Call Call { get; set; } = null!;

    public ReportType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Corps du rapport (texte libre, éventuellement pré-rempli par le masque de type).</summary>
    public string Body { get; set; } = string.Empty;

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    /// <summary>Destinataires (flags : Maire=1, Procureur=2, Préfecture=4, Hiérarchie=8, Service=16).</summary>
    public ReportRecipient Recipients { get; set; } = ReportRecipient.None;

    public bool IsDraft { get; set; } = true;
    public DateTime? FinalizedAt { get; set; }
}
