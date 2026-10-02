using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Hubs;

[Authorize]
public class PoliceHub(AppDbContext db, ILogger<PoliceHub> logger, IFlowLogService flowLog, INotificationCoordinator notificationCoordinator, IMissionService missionService) : Hub
{
    private Guid? TenantId => Guid.TryParse(Context.User?.FindFirst("tenantId")?.Value, out var t) ? t : null;
    private Guid? UserId => Guid.TryParse(Context.User?.FindFirst("userId")?.Value, out var u) ? u : null;

    /// <summary>
    /// Client -> Serveur : rejoindre le groupe d'une voiture spécifique.
    /// Appelé à la connexion initiale ET à chaque reconnexion automatique.
    /// Remet le véhicule en Available sauf si une mission est déjà acceptée/en cours,
    /// puis re-notifie les propositions en attente — en remplaçant une proposition
    /// de basse priorité par la mission la plus urgente si nécessaire.
    /// </summary>
    public async Task JoinVehicleGroup(string vehicleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vehicle_{vehicleId}");
        logger.LogInformation("[Hub] Connexion {ConnectionId} a rejoint le groupe vehicle_{VehicleId}",
            Context.ConnectionId, vehicleId);
        flowLog.Log("Server", "Information", "Hub",
            $"JoinVehicleGroup → groupe vehicle_{vehicleId}", TenantId, UserId,
            $"connId={Context.ConnectionId}");

        if (!Guid.TryParse(vehicleId, out var vid)) return;

        var vehicle = await db.PatrolVehicles.FindAsync(vid);
        if (vehicle is null)
        {
            flowLog.Log("Server", "Warning", "Hub", $"JoinVehicleGroup : véhicule {vehicleId} introuvable", TenantId, UserId);
            return;
        }

        var hasActiveMission = await db.MissionAssignments
            .AnyAsync(a => a.VehicleId == vid &&
                (a.Status == MissionStatus.Accepted || a.Status == MissionStatus.InProgress) &&
                a.Mission.Status != MissionStatus.Completed &&
                a.Mission.Status != MissionStatus.Cancelled);

        if (!hasActiveMission)
        {
            vehicle.Status = VehicleStatus.Available;
            await db.SaveChangesAsync();
            flowLog.Log("Server", "Information", "Hub", $"Véhicule {vehicle.CallSign} repassé Available", TenantId, UserId);
        }
        else
        {
            flowLog.Log("Server", "Information", "Hub",
                $"Véhicule {vehicle.CallSign} conserve son statut (mission active)", TenantId, UserId);
        }

        // Charger les propositions Proposed actuelles pour ce véhicule.
        var existingProposals = await db.MissionAssignments
            .Include(a => a.Mission)
            .Where(a => a.VehicleId == vid &&
                        a.Status == MissionStatus.Proposed &&
                        a.Mission.Status != MissionStatus.Completed &&
                        a.Mission.Status != MissionStatus.Cancelled)
            .ToListAsync();

        // Bug 1 (priorité) : si une mission en attente de priorité SUPÉRIEURE existe sans
        // assignation active, annuler les propositions actuelles de moindre priorité et
        // dispatcher la mission la plus urgente à ce véhicule.
        // La valeur sentinelle -1 permet de traiter le cas « aucune proposition » :
        // n'importe quelle mission en attente (priorité ≥ 0) est alors « plus haute ».
        if (!hasActiveMission)
        {
            var highestProposedPriority = existingProposals.Any()
                ? existingProposals.Max(a => (int)a.Mission.Priority)
                : -1;

            var urgentPending = await db.Missions
                .Where(m => m.TenantId == vehicle.TenantId
                    && m.Status == MissionStatus.Pending
                    && (int)m.Priority > highestProposedPriority
                    && !m.Assignments.Any(a =>
                        a.Status == MissionStatus.Proposed ||
                        a.Status == MissionStatus.Accepted ||
                        a.Status == MissionStatus.InProgress))
                .OrderByDescending(m => (int)m.Priority).ThenBy(m => m.CreatedAt)
                .FirstOrDefaultAsync();

            if (urgentPending != null)
            {
                // Annuler les propositions de moindre priorité pour libérer ce véhicule.
                foreach (var proposal in existingProposals)
                {
                    proposal.Status = MissionStatus.Refused;
                    proposal.RefusalReason = "Remplacée par une mission de priorité supérieure";
                    proposal.RefusalReasonCode = RefusalReasonCode.OnAnotherMission;
                    proposal.RespondedAt = DateTime.UtcNow;
                    flowLog.Log("Server", "Information", "Hub",
                        $"Proposition prio {(int)proposal.Mission.Priority} annulée (assignment={proposal.Id}) → mission urgente prio {(int)urgentPending.Priority}",
                        TenantId, UserId);
                }
                await db.SaveChangesAsync();

                try
                {
                    await missionService.ProposeToNextVehicleAsync(urgentPending.Id);
                    flowLog.Log("Server", "Information", "Hub",
                        $"Re-dispatch mission prioritaire {urgentPending.Id} (prio={(int)urgentPending.Priority}) à reconnexion",
                        TenantId, UserId);
                }
                catch (Exception ex)
                {
                    flowLog.Log("Server", "Warning", "Hub",
                        $"Échec re-dispatch mission prioritaire {urgentPending.Id}: {ex.Message}", TenantId, UserId);
                }

                // Recharger les propositions après le re-dispatch.
                existingProposals = await db.MissionAssignments
                    .Include(a => a.Mission)
                    .Where(a => a.VehicleId == vid &&
                                a.Status == MissionStatus.Proposed &&
                                a.Mission.Status != MissionStatus.Completed &&
                                a.Mission.Status != MissionStatus.Cancelled)
                    .ToListAsync();
            }
        }

        // Re-notifier les propositions en cours à cette connexion (Bug 3).
        foreach (var proposal in existingProposals)
        {
            await Clients.Caller.SendAsync("MissionProposed", new { assignmentId = proposal.Id });
            flowLog.Log("Server", "Information", "Hub",
                $"Re-send MissionProposed à reconnexion, assignment={proposal.Id}", TenantId, UserId);
        }
    }

    /// <summary>
    /// Client -> Serveur : rejoindre le groupe des opérateurs du tenant.
    /// </summary>
    public async Task JoinOperatorGroup()
    {
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;
        if (tenantId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"operators_{tenantId}");
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");
            flowLog.Log("Server", "Information", "Hub",
                $"JoinOperatorGroup → operators_{tenantId}", TenantId, UserId, $"connId={Context.ConnectionId}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;
        if (tenantId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        flowLog.Log("Server", "Information", "Hub",
            $"Connexion établie (rôle={role})", TenantId, UserId, $"connId={Context.ConnectionId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        flowLog.Log("Server", exception is null ? "Information" : "Warning", "Hub",
            $"Connexion fermée{(exception is null ? "" : $" : {exception.Message}")}",
            TenantId, UserId, $"connId={Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client -> Serveur : acquittement de réception d'une notification MissionProposed.
    /// Annule le timer Firebase de 15s côté serveur.
    /// </summary>
    public Task AckMissionNotification(Guid assignmentId)
    {
        notificationCoordinator.Ack(assignmentId);
        return Task.CompletedTask;
    }

    // --- Méthodes Serveur -> Client (noms des méthodes attendus côté client) ---
    // MissionProposed({ assignmentId })             — nouveau appel vers une voiture
    // VehiclePositionUpdated(VehiclePositionUpdate) — GPS update
    // MissionStatusChanged(MissionResponse)         — changement de statut
    // StreetRiskUpdated(StreetResponse)             — mise à jour risque rue
}
