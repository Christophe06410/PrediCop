using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Services;

/// <summary>
/// Singleton qui orchestre la notification d'une proposition de mission :
/// 1. SignalR immédiat si le mobile est connecté.
/// 2. Timer 15s : si pas d'ack, Firebase push en fallback.
/// </summary>
public class NotificationCoordinatorService(
    IHubContext<Hubs.PoliceHub> hub,
    IPushNotificationService push,
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationCoordinatorService> logger) : INotificationCoordinator
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pending = new();

    public async Task NotifyMissionProposedAsync(
        Guid assignmentId,
        Guid vehicleId,
        string firebaseTitle,
        string firebaseBody,
        CancellationToken ct = default)
    {
        // Annule un éventuel timer précédent pour ce même assignment (re-notification)
        if (_pending.TryRemove(assignmentId, out var old))
        {
            old.Cancel();
            old.Dispose();
        }

        // 1. SignalR immédiat — le mobile recharge les détails via l'API
        await hub.Clients
            .Group($"vehicle_{vehicleId}")
            .SendAsync("MissionProposed", new { assignmentId }, ct);

        logger.LogInformation("[Coordinator] MissionProposed SignalR → vehicle_{VehicleId}, assignment={AssignmentId}",
            vehicleId, assignmentId);

        // 2. Timer 15s : Firebase si pas d'ack
        var cts = new CancellationTokenSource();
        _pending[assignmentId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cts.Token);
                logger.LogInformation("[Coordinator] Pas d'ack pour assignment {Id} → Firebase fallback", assignmentId);
                await SendFirebaseFallbackAsync(vehicleId, firebaseTitle, firebaseBody);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[Coordinator] Ack reçu pour assignment {Id} → Firebase annulé", assignmentId);
            }
            finally
            {
                _pending.TryRemove(assignmentId, out _);
                cts.Dispose();
            }
        });
    }

    public void Ack(Guid assignmentId)
    {
        if (_pending.TryRemove(assignmentId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private async Task SendFirebaseFallbackAsync(Guid vehicleId, string title, string body)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var deviceTokens = await db.VehicleOfficers
            .Where(vo => vo.VehicleId == vehicleId && vo.IsActive)
            .Include(vo => vo.User)
            .Select(vo => vo.User.DeviceToken)
            .Where(t => t != null)
            .Cast<string>()
            .ToListAsync();

        if (deviceTokens.Count == 0) return;

        await push.SendToDevicesAsync(
            deviceTokens,
            title: title,
            body: body,
            data: new Dictionary<string, string>
            {
                { "type", "mission_proposed" }
            });
    }
}
