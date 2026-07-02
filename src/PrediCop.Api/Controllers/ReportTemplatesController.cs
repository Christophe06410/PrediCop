using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.DTOs;
using PrediCop.Core.Entities;
using PrediCop.Core.Enums;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Controllers;

[ApiController]
[Route("api/report-templates")]
[Authorize]
public class ReportTemplatesController(AppDbContext db) : ControllerBase
{
    private Guid TenantId => Guid.Parse(User.FindFirst("tenantId")!.Value);

    // ---- Masques par défaut (HTML) ------------------------------------------------

    private static readonly Dictionary<ReportType, (string Title, string Body)> Defaults = new()
    {
        [ReportType.Information] = (
            "Rapport d'information",
            """
            <p>Nous soussignés, <strong>[Noms des agents]</strong>, Agents de Police Municipale de <strong>[Commune]</strong>,<br>
            certifions avoir été informés / avoir constaté ce qui suit :</p>
            <p><strong>FAITS :</strong><br>[Description des faits]</p>
            <p><strong>MESURES PRISES :</strong><br>[Mesures prises]</p>
            <p>En foi de quoi, nous avons établi le présent rapport pour être transmis à [Destinataires].</p>
            <p>Fait à [Commune], le [Date]<br>[Signatures]</p>
            """
        ),
        [ReportType.Intervention] = (
            "Rapport d'intervention",
            """
            <p><strong>Rapport d'intervention N° [Référence]</strong></p>
            <p>Date et heure d'intervention : [Date et heure]<br>
            Lieu de l'intervention : [Adresse]<br>
            Motif : [Motif]</p>
            <p><strong>DÉROULEMENT DE L'INTERVENTION :</strong><br>[Description chronologique des faits]</p>
            <p><strong>PERSONNES RENCONTRÉES :</strong><br>[Identité et qualité des personnes]</p>
            <p><strong>MESURES PRISES :</strong><br>[Mesures adoptées]</p>
            <p>Fait à [Commune], le [Date]<br>[Signatures]</p>
            """
        ),
        [ReportType.CustodyTransfer] = (
            "Rapport de mise à disposition",
            """
            <p><strong>RAPPORT DE MISE À DISPOSITION</strong></p>
            <p>Nous soussignés, <strong>[Noms et grades des agents]</strong>, Agents de Police Municipale,</p>
            <p>Avons interpellé et mis à disposition de la Police Nationale / Gendarmerie :</p>
            <p><strong>IDENTITÉ DE LA PERSONNE :</strong><br>
            Nom :<br>Prénom :<br>Date de naissance :<br>Adresse :</p>
            <p><strong>MOTIF DE L'INTERPELLATION :</strong><br>[Motif détaillé]</p>
            <p><strong>CIRCONSTANCES :</strong><br>[Description des circonstances de l'interpellation]</p>
            <p>La personne a été remise à : [Service]<br>Le : [Date] à [Heure]</p>
            <p>Fait à [Commune], le [Date]<br>[Signatures]</p>
            """
        ),
        [ReportType.FormalRecord] = (
            "Procès-verbal",
            """
            <p><strong>PROCÈS-VERBAL N° [Référence]</strong></p>
            <p>L'an [Année], le [Date],<br>
            Nous soussignés, <strong>[Noms, grades et matricules des agents]</strong>,<br>
            Agents de Police Municipale de [Commune],</p>
            <p>Avons constaté les faits suivants :</p>
            <p><strong>OBJET :</strong><br>[Objet du procès-verbal]</p>
            <p><strong>CONSTATATIONS :</strong><br>[Constatations détaillées]</p>
            <p><strong>IDENTIFICATION DE L'AUTEUR :</strong><br>
            Nom :<br>Prénom :<br>Adresse :</p>
            <p><strong>SUITES DONNÉES :</strong><br>[Suites réservées à l'affaire]</p>
            <p>Fait à [Commune], le [Date]<br>[Signatures]</p>
            """
        ),
    };

    private static string TypeLabel(ReportType t) => t switch
    {
        ReportType.Information    => "Rapport d'information",
        ReportType.Intervention   => "Rapport d'intervention",
        ReportType.CustodyTransfer => "Rapport de mise à disposition",
        ReportType.FormalRecord   => "Procès-verbal",
        _                         => t.ToString(),
    };

    // ---- Endpoints ---------------------------------------------------------------

    /// <summary>Retourne les 4 masques du tenant (valeurs par défaut si non personnalisés).</summary>
    [HttpGet]
    public async Task<ActionResult<List<ReportTemplateResponse>>> GetAll(CancellationToken ct)
    {
        var stored = await db.ReportTemplates.Where(t => t.TenantId == TenantId).ToListAsync(ct);
        var storedByType = stored.ToDictionary(t => t.Type);

        return Enum.GetValues<ReportType>()
            .Select(type => Map(type, storedByType.GetValueOrDefault(type)))
            .ToList();
    }

    /// <summary>Retourne le masque pour un type donné.</summary>
    [HttpGet("{type}")]
    public async Task<ActionResult<ReportTemplateResponse>> GetByType(string type, CancellationToken ct)
    {
        if (!Enum.TryParse<ReportType>(type, ignoreCase: true, out var reportType))
            return BadRequest("Type inconnu.");

        var stored = await db.ReportTemplates
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Type == reportType, ct);

        return Map(reportType, stored);
    }

    /// <summary>Crée ou met à jour le masque pour un type donné.</summary>
    [HttpPut("{type}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<ReportTemplateResponse>> Upsert(
        string type, [FromBody] UpsertReportTemplateRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<ReportType>(type, ignoreCase: true, out var reportType))
            return BadRequest("Type inconnu.");

        var existing = await db.ReportTemplates
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Type == reportType, ct);

        if (existing is null)
        {
            existing = new ReportTemplate { Type = reportType, TenantId = TenantId };
            db.ReportTemplates.Add(existing);
        }

        existing.DefaultTitle = req.DefaultTitle;
        existing.Body         = req.Body;

        await db.SaveChangesAsync(ct);
        return Map(reportType, existing);
    }

    /// <summary>Remet le masque d'un type aux valeurs par défaut.</summary>
    [HttpDelete("{type}")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> Reset(string type, CancellationToken ct)
    {
        if (!Enum.TryParse<ReportType>(type, ignoreCase: true, out var reportType))
            return BadRequest("Type inconnu.");

        var existing = await db.ReportTemplates
            .FirstOrDefaultAsync(t => t.TenantId == TenantId && t.Type == reportType, ct);

        if (existing is not null)
        {
            db.ReportTemplates.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    // ---- Helpers -----------------------------------------------------------------

    private static ReportTemplateResponse Map(ReportType type, ReportTemplate? stored)
    {
        var (defaultTitle, defaultBody) = Defaults[type];
        return new ReportTemplateResponse
        {
            Id           = stored?.Id,
            Type         = type.ToString(),
            TypeLabel    = TypeLabel(type),
            DefaultTitle = stored?.DefaultTitle ?? defaultTitle,
            Body         = stored?.Body ?? defaultBody,
            IsCustomized = stored is not null,
            UpdatedAt    = stored?.UpdatedAt,
        };
    }
}
