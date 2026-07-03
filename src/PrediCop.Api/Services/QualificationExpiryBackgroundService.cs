using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;
using PrediCop.Infrastructure.Services;

namespace PrediCop.Api.Services;

/// <summary>
/// Envoie chaque lundi matin un email récapitulatif aux managers de chaque tenant
/// listant les habilitations expirées ou expirant dans les 30 prochains jours.
/// </summary>
public class QualificationExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<QualificationExpiryBackgroundService> logger) : BackgroundService
{
    // (tenantId → numéro de semaine ISO) pour ne pas envoyer deux fois dans la même semaine
    private readonly ConcurrentDictionary<Guid, int> _lastSentIsoWeek = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Fenêtre de tir : lundi entre 08h00 et 09h00 UTC (≈ 10h en France en été)
                if (now.DayOfWeek == DayOfWeek.Monday && now.Hour == 8)
                    await SendDigestToAllTenantsAsync(now, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du bilan hebdomadaire des habilitations");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task SendDigestToAllTenantsAsync(DateTime now, CancellationToken ct)
    {
        var currentWeek = ISOWeek.GetWeekOfYear(now);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db           = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var tenantIds = await db.Tenants
            .Where(t => !t.IsDeleted && t.SubscriptionStatus != SubscriptionStatus.Cancelled)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        foreach (var tenant in tenantIds)
        {
            // Éviter d'envoyer plusieurs fois dans la même semaine (ex: redémarrage à 8h45)
            if (_lastSentIsoWeek.TryGetValue(tenant.Id, out var lastWeek) && lastWeek == currentWeek)
                continue;

            try
            {
                await SendDigestForTenantAsync(db, emailService, tenant.Id, tenant.Name, now, ct);
                _lastSentIsoWeek[tenant.Id] = currentWeek;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur bilan habilitations pour tenant {TenantId}", tenant.Id);
            }
        }
    }

    private async Task SendDigestForTenantAsync(
        AppDbContext db,
        IEmailService emailService,
        Guid tenantId,
        string tenantName,
        DateTime now,
        CancellationToken ct)
    {
        var threshold = now.AddDays(30);

        var qualifications = await db.AgentQualifications
            .Include(q => q.Agent)
            .Where(q => q.TenantId == tenantId && q.ExpiresAt < threshold)
            .OrderBy(q => q.ExpiresAt)
            .ToListAsync(ct);

        if (qualifications.Count == 0)
            return;

        static string TypeLabel(QualificationType t) => t switch
        {
            QualificationType.APJA                    => "APJA",
            QualificationType.PorteArme               => "Port d'arme",
            QualificationType.PermisConduire          => "Permis de conduire",
            QualificationType.FormationSecours        => "Formation secours",
            QualificationType.HabilitationPrefectorale => "Habilitation préfect.",
            _                                          => "Autre"
        };

        var items = qualifications
            .Select(q => (
                q.Agent.FullName,
                q.Agent.BadgeNumber,
                TypeLabel(q.Type),
                q.Reference,
                q.ExpiresAt,
                q.IsExpired))
            .ToList();

        var expiredCount  = items.Count(i => i.IsExpired);
        var expiringCount = items.Count - expiredCount;

        var parts = new List<string>();
        if (expiredCount  > 0) parts.Add($"{expiredCount} expirée(s)");
        if (expiringCount > 0) parts.Add($"{expiringCount} à renouveler");
        var subject = $"[PrediCop] Habilitations agents — {string.Join(", ", parts)}";

        var htmlBody = EmailTemplates.HabilitationsExpiration(tenantName, items);

        await emailService.SendToManagersAsync(tenantId, subject, htmlBody, ct);

        logger.LogInformation(
            "Bilan habilitations envoyé pour tenant {TenantId} : {Expired} expirée(s), {Expiring} à renouveler",
            tenantId, expiredCount, expiringCount);
    }
}
