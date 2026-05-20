using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PrediCop.Api.Hubs;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;
using PrediCop.Infrastructure.Services;
using System.Collections.Concurrent;

namespace PrediCop.Api.Services;

/// <summary>
/// Service de fond qui vérifie toutes les 2 minutes si des véhicules de patrouille
/// ont quitté leur zone assignée (géofencing) et envoie des alertes si nécessaire.
/// </summary>
public class GeofencingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IHubContext<PoliceHub> hubContext,
    IEmailService emailService,
    ILogger<GeofencingBackgroundService> logger) : BackgroundService
{
    private const int CheckIntervalMinutes = 2;
    private const int AlertCooldownMinutes = 30;
    private const int GpsStaleMinutes = 10;

    /// <summary>Anti-spam: stocke la dernière alerte envoyée par véhicule.</summary>
    private readonly ConcurrentDictionary<Guid, DateTime> _lastAlertSent = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Attendre que l'application soit complètement démarrée
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckVehiclesGeofencingAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la vérification du géofencing des véhicules");
            }

            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken);
        }
    }

    private async Task CheckVehiclesGeofencingAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Récupérer tous les tenants avec le géofencing activé
        var enabledTenants = await db.Tenants
            .Where(t => t.GeofencingEnabled && t.IsActive)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (enabledTenants.Count == 0)
            return;

        var gpsThreshold = DateTime.UtcNow.AddMinutes(-GpsStaleMinutes);
        var now = DateTime.UtcNow;

        // Purge des entrées expirées pour limiter la mémoire
        var expiredKeys = _lastAlertSent
            .Where(kv => kv.Value < now.AddMinutes(-AlertCooldownMinutes))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in expiredKeys)
            _lastAlertSent.TryRemove(key, out _);

        foreach (var tenantId in enabledTenants)
        {
            try
            {
                await CheckTenantVehiclesAsync(db, tenantId, gpsThreshold, now, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur géofencing pour le tenant {TenantId}", tenantId);
            }
        }
    }

    private async Task CheckTenantVehiclesAsync(
        AppDbContext db,
        Guid tenantId,
        DateTime gpsThreshold,
        DateTime now,
        CancellationToken ct)
    {
        // Véhicules avec une zone assignée et un GPS récent
        var vehicles = await db.PatrolVehicles
            .Include(v => v.AssignedGeoZone)
                .ThenInclude(z => z!.Vertices)
            .Where(v => v.TenantId == tenantId
                        && v.AssignedGeoZoneId != null
                        && v.LastLatitude != null
                        && v.LastLongitude != null
                        && v.LastPositionUpdate != null
                        && v.LastPositionUpdate > gpsThreshold)
            .ToListAsync(ct);

        foreach (var vehicle in vehicles)
        {
            if (vehicle.AssignedGeoZone is null || vehicle.AssignedGeoZone.Vertices.Count < 3)
                continue;

            var vertices = vehicle.AssignedGeoZone.Vertices
                .OrderBy(v => v.Order)
                .Select(v => (v.Latitude, v.Longitude))
                .ToList();

            bool isInside = GeofencingService.IsInsidePolygon(
                vehicle.LastLatitude!.Value,
                vehicle.LastLongitude!.Value,
                vertices);

            if (isInside)
                continue;

            // Véhicule hors zone — vérifier l'anti-spam
            if (_lastAlertSent.TryGetValue(vehicle.Id, out var lastSent)
                && (now - lastSent).TotalMinutes < AlertCooldownMinutes)
            {
                continue;
            }

            var zoneName = vehicle.AssignedGeoZone.Name;
            var detectedAt = now;

            logger.LogWarning(
                "Véhicule {CallSign} (Id={VehicleId}) hors de la zone '{ZoneName}' (tenant {TenantId})",
                vehicle.CallSign, vehicle.Id, zoneName, tenantId);

            // Broadcast SignalR vers tous les opérateurs du tenant
            await hubContext.Clients
                .Group($"operators_{tenantId}")
                .SendAsync("VehicleOutOfZone", new
                {
                    VehicleId = vehicle.Id,
                    CallSign = vehicle.CallSign,
                    GeoZoneName = zoneName,
                    DetectedAt = detectedAt
                }, ct);

            // Alerte email aux managers
            var subject = $"[PrediCop] Véhicule {vehicle.CallSign} hors zone";
            var htmlBody = BuildOutOfZoneEmailBody(vehicle.CallSign, zoneName, detectedAt);

            try
            {
                await emailService.SendToManagersAsync(tenantId, subject, htmlBody, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de l'envoi de l'alerte email pour le véhicule {VehicleId}", vehicle.Id);
            }

            _lastAlertSent[vehicle.Id] = now;
        }
    }

    private static string BuildOutOfZoneEmailBody(string callSign, string zoneName, DateTime detectedAt)
    {
        var detectedAtLocal = detectedAt.ToString("dd/MM/yyyy HH:mm:ss") + " UTC";
        return $"""
            <!DOCTYPE html>
            <html lang="fr">
            <head><meta charset="utf-8" /></head>
            <body style="font-family: Arial, sans-serif; color: #333;">
                <div style="max-width: 600px; margin: 0 auto; border: 1px solid #ddd; border-radius: 8px; overflow: hidden;">
                    <div style="background: #dc3545; color: #fff; padding: 20px;">
                        <h2 style="margin: 0;">&#9888; Alerte géofencing — Véhicule hors zone</h2>
                    </div>
                    <div style="padding: 24px;">
                        <p>Un véhicule de patrouille a été détecté <strong>hors de sa zone assignée</strong>.</p>
                        <table style="width: 100%; border-collapse: collapse; margin-top: 16px;">
                            <tr style="background: #f8f9fa;">
                                <td style="padding: 10px 14px; font-weight: bold; border-bottom: 1px solid #dee2e6;">Véhicule</td>
                                <td style="padding: 10px 14px; border-bottom: 1px solid #dee2e6;">{callSign}</td>
                            </tr>
                            <tr>
                                <td style="padding: 10px 14px; font-weight: bold; border-bottom: 1px solid #dee2e6;">Zone attendue</td>
                                <td style="padding: 10px 14px; border-bottom: 1px solid #dee2e6;">{zoneName}</td>
                            </tr>
                            <tr style="background: #f8f9fa;">
                                <td style="padding: 10px 14px; font-weight: bold;">Détecté à</td>
                                <td style="padding: 10px 14px;">{detectedAtLocal}</td>
                            </tr>
                        </table>
                        <p style="margin-top: 20px; color: #6c757d; font-size: 0.9em;">
                            Ce message est généré automatiquement par PrediCop.<br/>
                            Une nouvelle alerte sera envoyée si la situation persiste après 30 minutes.
                        </p>
                    </div>
                    <div style="background: #1a2035; color: #a0aec0; padding: 12px 24px; font-size: 0.8em;">
                        PrediCop &mdash; SaaS de gestion de Police Municipale
                    </div>
                </div>
            </body>
            </html>
            """;
    }
}
