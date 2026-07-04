using System.Net;

namespace PrediCop.Infrastructure.Services;

public static class EmailTemplates
{
    // ── Layout de base (header/footer commun) ─────────────────────────────────

    public static string WrapInLayout(string tenantName, string content) => $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><title>PrediCop</title></head>
        <body style="margin:0;padding:0;background:#f0f4f8;font-family:Arial,Helvetica,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f4f8;padding:20px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.1);">
                <!-- Header -->
                <tr><td style="background:#1a2035;padding:24px 32px;">
                  <h1 style="color:#ffffff;margin:0;font-size:22px;">&#128660; PrediCop</h1>
                  <p style="color:#94a3b8;margin:4px 0 0;font-size:13px;">{WebUtility.HtmlEncode(tenantName)}</p>
                </td></tr>
                <!-- Content -->
                <tr><td style="padding:32px;">{content}</td></tr>
                <!-- Footer -->
                <tr><td style="background:#f8fafc;padding:16px 32px;text-align:center;">
                  <p style="color:#94a3b8;font-size:12px;margin:0;">PrediCop — Gestion de Police Municipale | Message généré automatiquement.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    // ── Template : document transmis (Parquet / Juge) ─────────────────────────

    public static string DocumentTransmis(
        string tenantName,
        string documentRef,
        string documentTitle,
        string missionRef,
        string destinataire,
        string changedBy,
        DateTime changedAt)
    {
        var content = $"""
            <h2 style="color:#2563eb;font-size:20px;margin:0 0 8px;">
                Document transmis {WebUtility.HtmlEncode(destinataire)}
            </h2>
            <p style="color:#475569;margin:0 0 24px;font-size:14px;">
                Le document ci-dessous vient d'être transmis <strong>{WebUtility.HtmlEncode(destinataire)}</strong>.
            </p>

            <table style="width:100%;border-collapse:collapse;font-size:14px;margin-bottom:24px;">
              <tr style="background:#eff6ff;">
                <td style="padding:10px 14px;font-weight:bold;color:#1e40af;width:38%;border-bottom:1px solid #dbeafe;">Référence</td>
                <td style="padding:10px 14px;color:#1e3a8a;font-weight:bold;border-bottom:1px solid #dbeafe;">{WebUtility.HtmlEncode(documentRef)}</td>
              </tr>
              <tr>
                <td style="padding:10px 14px;font-weight:bold;color:#374151;border-bottom:1px solid #f1f5f9;">Titre</td>
                <td style="padding:10px 14px;color:#374151;border-bottom:1px solid #f1f5f9;">{WebUtility.HtmlEncode(documentTitle)}</td>
              </tr>
              <tr style="background:#f8fafc;">
                <td style="padding:10px 14px;font-weight:bold;color:#374151;border-bottom:1px solid #f1f5f9;">Mission associée</td>
                <td style="padding:10px 14px;color:#374151;border-bottom:1px solid #f1f5f9;">{WebUtility.HtmlEncode(missionRef.Length > 0 ? missionRef : "N/A")}</td>
              </tr>
              <tr>
                <td style="padding:10px 14px;font-weight:bold;color:#374151;border-bottom:1px solid #f1f5f9;">Transmis par</td>
                <td style="padding:10px 14px;color:#374151;border-bottom:1px solid #f1f5f9;">{WebUtility.HtmlEncode(changedBy)}</td>
              </tr>
              <tr style="background:#f8fafc;">
                <td style="padding:10px 14px;font-weight:bold;color:#374151;">Date &amp; heure</td>
                <td style="padding:10px 14px;color:#374151;">{changedAt:dd/MM/yyyy HH:mm} UTC</td>
              </tr>
            </table>

            <div style="background:#eff6ff;border-left:4px solid #2563eb;padding:12px 16px;border-radius:0 4px 4px 0;">
              <p style="margin:0;font-size:13px;color:#1e40af;">
                Connectez-vous au back-office PrediCop pour consulter le document complet.
              </p>
            </div>
            """;

        return WrapInLayout(tenantName, content);
    }

    // ── Template : bilan hebdomadaire des habilitations ──────────────────────

    public static string HabilitationsExpiration(
        string tenantName,
        IReadOnlyList<(string AgentFullName, string BadgeNumber, string TypeLabel, string Reference, DateTime ExpiresAt, bool IsExpired)> items)
    {
        static string Row(string agent, string badge, string type, string reference, DateTime expires, bool expired)
        {
            var color = expired ? "#991b1b" : "#92400e";
            var bg    = expired ? "#fef2f2" : "#fffbeb";
            var days  = (int)(expires - DateTime.UtcNow).TotalDays;
            var daysLabel = expired
                ? $"Expirée depuis {-days} jour(s)"
                : $"Dans {days} jour(s)";

            return $"""
                <tr style="background:{bg};">
                  <td style="padding:9px 12px;font-weight:bold;color:{color};border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(agent)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(badge)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(type)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(reference)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{expires:dd/MM/yyyy}</td>
                  <td style="padding:9px 12px;font-weight:bold;color:{color};border-bottom:1px solid #fde68a;">{daysLabel}</td>
                </tr>
                """;
        }

        var rows = string.Join("\n", items.Select(i =>
            Row(i.AgentFullName, i.BadgeNumber, i.TypeLabel, i.Reference, i.ExpiresAt, i.IsExpired)));

        var expiredCount  = items.Count(i => i.IsExpired);
        var expiringCount = items.Count(i => !i.IsExpired);

        var summary = new List<string>();
        if (expiredCount  > 0) summary.Add($"<strong style='color:#dc2626;'>{expiredCount} expirée(s)</strong>");
        if (expiringCount > 0) summary.Add($"<strong style='color:#d97706;'>{expiringCount} à renouveler dans 30 jours</strong>");

        var content = $"""
            <h2 style="color:#1a2035;font-size:20px;margin:0 0 8px;">
                &#128196; Bilan hebdomadaire des habilitations
            </h2>
            <p style="color:#475569;margin:0 0 24px;font-size:14px;">
                Ce rapport recense les habilitations des agents nécessitant votre attention :
                {string.Join(" — ", summary)}.
            </p>

            <table style="width:100%;border-collapse:collapse;font-size:13px;margin-bottom:24px;">
              <thead>
                <tr style="background:#1a2035;">
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Agent</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Badge</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Type</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Référence</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Expiration</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Délai</th>
                </tr>
              </thead>
              <tbody>
                {rows}
              </tbody>
            </table>

            <div style="background:#eff6ff;border-left:4px solid #2563eb;padding:12px 16px;border-radius:0 4px 4px 0;">
              <p style="margin:0;font-size:13px;color:#1e40af;">
                Connectez-vous au back-office PrediCop → <em>Admin / Habilitations</em> pour mettre à jour ces certifications.
              </p>
            </div>
            """;

        return WrapInLayout(tenantName, content);
    }

    // ── Template : mission sans véhicule accepteur ────────────────────────────

    public static string MissionSansVehicule(
        string tenantName,
        string missionRef,
        string targetAddress,
        int minutesElapsed,
        int proposalsCount)
    {
        var urgenceColor = minutesElapsed >= 30 ? "#dc2626" : "#f59e0b";
        var urgenceLabel = minutesElapsed >= 30 ? "CRITIQUE" : "ALERTE";

        var content = $"""
            <div style="background:#fef2f2;border:1px solid #fecaca;border-radius:6px;padding:16px 20px;margin-bottom:24px;">
              <h2 style="color:#dc2626;font-size:18px;margin:0 0 6px;">
                &#9888; Mission en attente d'affectation
              </h2>
              <p style="color:#7f1d1d;margin:0;font-size:14px;">
                Aucun véhicule n'a accepté cette mission depuis <strong>{minutesElapsed} minutes</strong>.
                Une intervention manuelle peut être nécessaire.
              </p>
            </div>

            <table style="width:100%;border-collapse:collapse;font-size:14px;margin-bottom:24px;">
              <tr style="background:#fef2f2;">
                <td style="padding:10px 14px;font-weight:bold;color:#991b1b;width:38%;border-bottom:1px solid #fecaca;">Référence mission</td>
                <td style="padding:10px 14px;color:#7f1d1d;font-weight:bold;border-bottom:1px solid #fecaca;">{WebUtility.HtmlEncode(missionRef)}</td>
              </tr>
              <tr>
                <td style="padding:10px 14px;font-weight:bold;color:#374151;border-bottom:1px solid #f1f5f9;">Adresse cible</td>
                <td style="padding:10px 14px;color:#374151;border-bottom:1px solid #f1f5f9;">{WebUtility.HtmlEncode(targetAddress)}</td>
              </tr>
              <tr style="background:#f8fafc;">
                <td style="padding:10px 14px;font-weight:bold;color:#374151;border-bottom:1px solid #f1f5f9;">En attente depuis</td>
                <td style="padding:10px 14px;color:#374151;border-bottom:1px solid #f1f5f9;">{minutesElapsed} minutes</td>
              </tr>
              <tr>
                <td style="padding:10px 14px;font-weight:bold;color:#374151;">Propositions envoyées</td>
                <td style="padding:10px 14px;color:#374151;">{proposalsCount} véhicule(s)</td>
              </tr>
            </table>

            <div style="background:{urgenceColor};border-radius:4px;padding:12px 16px;text-align:center;margin-bottom:16px;">
              <p style="margin:0;font-size:14px;font-weight:bold;color:#ffffff;">
                Statut : {urgenceLabel} — Affectez manuellement un véhicule disponible
              </p>
            </div>

            <div style="background:#f0fdf4;border-left:4px solid #16a34a;padding:12px 16px;border-radius:0 4px 4px 0;">
              <p style="margin:0;font-size:13px;color:#15803d;">
                Connectez-vous au back-office PrediCop pour dispatcher cette mission manuellement.
              </p>
            </div>
            """;

        return WrapInLayout(tenantName, content);
    }

    // ── Template : bilan hebdomadaire des entretiens flotte ───────────────────

    public static string MaintenanceFleet(
        string tenantName,
        IReadOnlyList<(string CallSign, string LicensePlate, string TypeLabel, string Description, DateTime ScheduledDate, bool IsOverdue)> items)
    {
        static string Row(string callSign, string plate, string type, string desc, DateTime date, bool overdue)
        {
            var color = overdue ? "#991b1b" : "#92400e";
            var bg    = overdue ? "#fef2f2" : "#fffbeb";
            var days  = (int)(date - DateTime.UtcNow).TotalDays;
            var daysLabel = overdue
                ? $"En retard de {-days} jour(s)"
                : days == 0 ? "Aujourd'hui" : $"Dans {days} jour(s)";

            return $"""
                <tr style="background:{bg};">
                  <td style="padding:9px 12px;font-weight:bold;color:{color};border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(callSign)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(plate)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(type)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{WebUtility.HtmlEncode(desc)}</td>
                  <td style="padding:9px 12px;color:#374151;border-bottom:1px solid #fde68a;">{date:dd/MM/yyyy}</td>
                  <td style="padding:9px 12px;font-weight:bold;color:{color};border-bottom:1px solid #fde68a;">{daysLabel}</td>
                </tr>
                """;
        }

        var rows = string.Join("\n", items.Select(i =>
            Row(i.CallSign, i.LicensePlate, i.TypeLabel, i.Description, i.ScheduledDate, i.IsOverdue)));

        var overdueCount  = items.Count(i => i.IsOverdue);
        var upcomingCount = items.Count(i => !i.IsOverdue);

        var summary = new List<string>();
        if (overdueCount  > 0) summary.Add($"<strong style='color:#dc2626;'>{overdueCount} en retard</strong>");
        if (upcomingCount > 0) summary.Add($"<strong style='color:#d97706;'>{upcomingCount} à planifier dans 30 jours</strong>");

        var content = $"""
            <h2 style="color:#1a2035;font-size:20px;margin:0 0 8px;">
                &#128663; Bilan hebdomadaire — Entretiens véhicules
            </h2>
            <p style="color:#475569;margin:0 0 24px;font-size:14px;">
                Ce rapport recense les entretiens nécessitant votre attention :
                {string.Join(" — ", summary)}.
            </p>

            <table style="width:100%;border-collapse:collapse;font-size:13px;margin-bottom:24px;">
              <thead>
                <tr style="background:#1a2035;">
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Véhicule</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Immatriculation</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Type</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Description</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Date prévue</th>
                  <th style="padding:10px 12px;color:#ffffff;text-align:left;">Délai</th>
                </tr>
              </thead>
              <tbody>
                {rows}
              </tbody>
            </table>

            <div style="background:#eff6ff;border-left:4px solid #2563eb;padding:12px 16px;border-radius:0 4px 4px 0;">
              <p style="margin:0;font-size:13px;color:#1e40af;">
                Connectez-vous au back-office PrediCop → <em>Admin / Gestion de flotte</em> pour planifier ces entretiens.
              </p>
            </div>
            """;

        return WrapInLayout(tenantName, content);
    }
}
