using Microsoft.EntityFrameworkCore;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;
using PrediCop.Infrastructure.Services;
using System.Collections.Concurrent;

namespace PrediCop.Api.Services;

public class MissionAlertBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MissionAlertBackgroundService> logger) : BackgroundService
{
    private const int CheckIntervalMinutes   = 5;
    private const int AlertThresholdMinutes  = 15;
    private const int AlertCooldownMinutes   = 60;

    // Tracks the last alert time per mission to avoid spamming
    private readonly ConcurrentDictionary<Guid, DateTime> _lastAlertSent = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the application to fully start before the first check
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckPendingMissionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la vérification des missions en attente");
            }

            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken);
        }
    }

    private async Task CheckPendingMissionsAsync(CancellationToken ct)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-AlertThresholdMinutes);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db           = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // Missions Pending ou Proposed depuis plus de AlertThresholdMinutes minutes
        var pendingMissions = await db.Missions
            .Include(m => m.Assignments)
            .Where(m => (m.Status == MissionStatus.Pending || m.Status == MissionStatus.Proposed)
                        && m.CreatedAt < threshold)
            .ToListAsync(ct);

        if (pendingMissions.Count == 0)
            return;

        logger.LogInformation(
            "{Count} mission(s) en attente depuis plus de {Threshold} min détectée(s)",
            pendingMissions.Count, AlertThresholdMinutes);

        // Purge expired cooldown entries to keep memory usage low
        var expiredKeys = _lastAlertSent
            .Where(kv => kv.Value < DateTime.UtcNow.AddMinutes(-AlertCooldownMinutes))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in expiredKeys)
            _lastAlertSent.TryRemove(key, out _);

        var now = DateTime.UtcNow;

        foreach (var mission in pendingMissions)
        {
            // Skip if an alert was already sent within the cooldown window
            if (_lastAlertSent.TryGetValue(mission.Id, out var lastSent)
                && (now - lastSent).TotalMinutes < AlertCooldownMinutes)
            {
                continue;
            }

            try
            {
                var tenantName = await db.Tenants
                    .Where(t => t.Id == mission.TenantId)
                    .Select(t => t.Name)
                    .FirstOrDefaultAsync(ct) ?? "Police Municipale";

                var minutesElapsed = (int)(now - mission.CreatedAt).TotalMinutes;
                var proposalsCount = mission.Assignments.Count;

                var subject = $"[PrediCop] Mission {mission.Reference} sans véhicule accepteur depuis {minutesElapsed} min";

                var htmlBody = EmailTemplates.MissionSansVehicule(
                    tenantName:     tenantName,
                    missionRef:     mission.Reference,
                    targetAddress:  mission.TargetAddress,
                    minutesElapsed: minutesElapsed,
                    proposalsCount: proposalsCount
                );

                await emailService.SendToManagersAsync(mission.TenantId, subject, htmlBody, ct);

                _lastAlertSent[mission.Id] = now;

                logger.LogWarning(
                    "Alerte email envoyée pour la mission {MissionRef} (tenant {TenantId}, {Minutes} min, {Proposals} proposition(s))",
                    mission.Reference, mission.TenantId, minutesElapsed, proposalsCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de l'envoi de l'alerte pour la mission {MissionId}", mission.Id);
            }
        }
    }
}
