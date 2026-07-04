using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;
using PrediCop.Infrastructure.Services;

namespace PrediCop.Api.Services;

/// <summary>
/// Envoie chaque lundi matin un email récapitulatif aux managers de chaque tenant (fleet activé)
/// listant les entretiens véhicules en retard ou à venir dans les 30 prochains jours.
/// </summary>
public class FleetMaintenanceAlertBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<FleetMaintenanceAlertBackgroundService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, int> _lastSentIsoWeek = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(6), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                if (now.DayOfWeek == DayOfWeek.Monday && now.Hour == 8)
                    await SendDigestToAllTenantsAsync(now, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors du bilan hebdomadaire des entretiens flotte");
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

        var tenants = await db.Tenants
            .Where(t => !t.IsDeleted
                     && t.SubscriptionStatus != SubscriptionStatus.Cancelled
                     && t.ModuleFleetEnabled)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            if (_lastSentIsoWeek.TryGetValue(tenant.Id, out var lastWeek) && lastWeek == currentWeek)
                continue;

            try
            {
                await SendDigestForTenantAsync(db, emailService, tenant.Id, tenant.Name, now, ct);
                _lastSentIsoWeek[tenant.Id] = currentWeek;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur bilan flotte pour tenant {TenantId}", tenant.Id);
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

        var maintenances = await db.VehicleMaintenances
            .Include(m => m.Vehicle)
            .Where(m => m.TenantId == tenantId
                     && !m.IsCompleted
                     && m.ScheduledDate < threshold)
            .OrderBy(m => m.ScheduledDate)
            .ToListAsync(ct);

        if (maintenances.Count == 0)
            return;

        static string TypeLabel(MaintenanceType t) => t switch
        {
            MaintenanceType.Revision          => "Révision",
            MaintenanceType.ControleTechnique => "Contrôle technique",
            MaintenanceType.Reparation        => "Réparation",
            MaintenanceType.Nettoyage         => "Nettoyage",
            MaintenanceType.Pneumatiques      => "Pneumatiques",
            MaintenanceType.Carrosserie       => "Carrosserie",
            _                                 => "Autre"
        };

        var items = maintenances
            .Select(m => (
                m.Vehicle.CallSign,
                m.Vehicle.LicensePlate,
                TypeLabel(m.Type),
                m.Description,
                m.ScheduledDate,
                IsOverdue: m.ScheduledDate < now))
            .ToList();

        var overdueCount  = items.Count(i => i.IsOverdue);
        var upcomingCount = items.Count - overdueCount;

        var parts = new List<string>();
        if (overdueCount  > 0) parts.Add($"{overdueCount} en retard");
        if (upcomingCount > 0) parts.Add($"{upcomingCount} à planifier");
        var subject = $"[PrediCop] Entretiens véhicules — {string.Join(", ", parts)}";

        var htmlBody = EmailTemplates.MaintenanceFleet(tenantName, items);

        await emailService.SendToManagersAsync(tenantId, subject, htmlBody, ct);

        logger.LogInformation(
            "Bilan flotte envoyé pour tenant {TenantId} : {Overdue} en retard, {Upcoming} à venir",
            tenantId, overdueCount, upcomingCount);
    }
}
