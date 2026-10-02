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

    private static readonly string MarianneDataUrl = "data:image/png;base64,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";

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
            <table style="width:100%; border:none; border-collapse:collapse; margin-bottom:0.3em">
              <tr>
                <td style="border:none; text-align:left; width:33%"><strong>Département de …</strong></td>
                <td style="border:none; text-align:center; width:34%"><strong>République française</strong></td>
                <td style="border:none; text-align:right; width:33%"><strong>Ville de …</strong></td>
              </tr>
            </table>
            <p style="text-align:center">Le [date]</p>
            <p style="text-align:center; margin-top:1em"><strong>RAPPORT</strong></p>
            <p>Le gardien-brigadier de police municipale <strong>[Nom du rédacteur]</strong></p>
            <p>à Monsieur/Madame le Maire de (la ville de) …<br>
            Monsieur/Madame le Procureur de la République.</p>
            <p><strong>Objet :</strong> </p>
            <p><strong>Pièces jointes :</strong> </p>
            <p>Nous, gardien-brigadier de police municipale <strong>[Nom]</strong> en fonction dans la commune de …, dûment agréé et assermenté, agissant en tenue réglementaire conformément aux ordres reçus et aux dispositions des articles 21, 21-2 et… du code de procédure pénale, avons l'honneur de vous rendre compte des faits suivants :</p>
            <p>…………………………………………………………………………………………………………………………………………………………………</p>
            <p><strong>Identité des victimes</strong> (nom, prénom, date et lieu de naissance, adresse)<br>-<br>-</p>
            <p><strong>Identité des mis en cause :</strong><br>-<br>-</p>
            <p><strong>Identité des témoins :</strong><br>-<br>-</p>
            <p>Fait et clos à (lieu), le (date) à (heure)</p>
            <p>Le gardien/brigadier de police municipale [Nom]</p>
            <p><strong>Transmissions</strong><br>
            M./Mme l'Officier de Police Judiciaire territorialement compétent.<br>
            M./Mme le Maire.<br>
            M./Mme le responsable de la Police Municipale.<br>
            Archives municipales.</p>
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
            $"""
            <table style="width:100%; border-collapse:collapse; border:1px solid black">
              <tr>
                <td style="border-right:1px solid black; padding:8px; width:23%; vertical-align:top; text-align:center; font-size:9pt">
                  <img src="{MarianneDataUrl}" alt="République Française" style="width:130px; height:auto; display:block; margin:0 auto 8px auto"/>
                  Département du …<br>
                  <br>
                  <strong>MAIRIE de [Ville]</strong><br>
                  <br>
                  <strong>POLICE MUNICIPALE</strong><br>
                  [Rue]<br>
                  [Code postal] [Ville]<br>
                  Téléphone : [N° tél]<br>
                  <br>
                  <strong>RÉFÉRENCES</strong><br>
                  <br>
                  <div style="text-align:left">
                    Rapport :&nbsp;<br>
                    Affaire :&nbsp;<br>
                    Feuillet :&nbsp;<br>
                    P/jointes :&nbsp;
                  </div>
                  <br>
                  <div style="background:#cccccc; padding:5px; text-align:center; font-weight:bold; border:1px solid #999; margin-top:4px">OBJET</div>
                  <br><br><br><br>
                </td>
                <td style="padding:8px; vertical-align:top">
                  <p style="text-align:center; margin:0 0 0.2em 0"><strong>REPUBLIQUE FRANÇAISE</strong></p>
                  <p style="text-align:center; font-size:20pt; margin:0 0 1em 0"><strong>RAPPORT</strong></p>
                  <p style="text-align:justify">L'an deux mille [année], le … du mois de …</p>
                  <p style="text-align:justify">Nous, Gardien de Police Municipale <strong>[Nom]</strong> matricule <strong>[N°]</strong>, assisté du Gardien de Police <strong>[Nom]</strong>, matricule <strong>[N°]</strong>, agents de police judiciaire adjoints, agréés et assermentés en résidence administrative à la Police Municipale de [Ville].</p>
                  <p style="text-align:justify">Vu les articles L.511-1 à L.515-1 du Code de la Sécurité Intérieure<br>
                  Vu les articles 21/2°, 21-2, D15, du Code de Procédure Pénale</p>
                  <p style="text-align:justify">Rapportons les faits suivants agissant revêtus de notre uniforme réglementaire et conformément aux ordres reçus.</p>
                  <p style="text-align:justify">Ce jour, à … heures et … minutes de patrouille</p>
                  <p>&nbsp;</p>
                  <p>&nbsp;</p>
                  <p>Le rédacteur :&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; L'assistant :</p>
                </td>
              </tr>
            </table>
            <br>
            <table style="width:100%; border-collapse:collapse; border:1px solid black; margin-top:1em">
              <tr>
                <td style="border-right:1px solid black; padding:8px; width:25%; vertical-align:top; font-size:9pt">
                  <div style="text-align:center; text-decoration:underline; font-weight:bold; margin-bottom:6px">RÉFÉRENCES</div>
                  <strong>Rapport :</strong><br>
                  <strong>Affaire :</strong><br>
                  <strong>Feuillet :</strong><br>
                  <strong>P/jointes :</strong>
                </td>
                <td style="padding:0; vertical-align:top; font-size:9pt">
                  <!-- Identité en texte (haut) -->
                  <div style="padding:6px; border-bottom:1px solid black">
                    <strong>IDENTITE :</strong><br>
                    Nom :<br>
                    Prénom :<br>
                    Date de naissance :<br>
                    Adresse :<br>
                    &nbsp;<br>
                    N° de téléphone :
                  </div>
                  <!-- Destinataire(s) + Transmissions (bas) : sous-table 2 colonnes -->
                  <table style="width:100%; border-collapse:collapse">
                    <tr>
                      <td style="padding:4px; font-size:9pt; font-weight:bold" colspan="2">Destinataire(s)</td>
                    </tr>
                    <tr>
                      <td style="border-right:1px solid black; padding:6px; vertical-align:top; font-size:9pt; width:60%">
                        ☐ <strong>ORIGINAL</strong> transmis à Monsieur le Procureur de la République par l'intermédiaire de l'Officier de Police Judiciaire territorialement compétent.<br><br>
                        ☐ <strong>Copie</strong> transmise à Monsieur le Maire<br><br>
                        ☐ <strong>Copie</strong> transmise à Monsieur le responsable de la Police Municipale de [lieu]<br><br>
                        ☐ <strong>Copie</strong> conservée aux archives de la Police Municipale
                      </td>
                      <td style="padding:6px; vertical-align:top; font-size:9pt">
                        Vu pour être transmis à Monsieur le Procureur de la République<br>
                        <br><br><br>
                        Le responsable de la Police Municipale<br>
                        <br><br><br>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
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
