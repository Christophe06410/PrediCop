using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrediCop.Core.Enums;
using PrediCop.Core.Interfaces;
using PrediCop.Infrastructure.Data;

namespace PrediCop.Api.Hubs;

[Authorize]
public class PoliceHub(AppDbContext db, ILogger<PoliceHub> logger, IFlowLogService flowLog) : Hub
{
    private Guid? TenantId => Guid.TryParse(Context.User?.FindFirst("tenantId")?.Value, out var t) ? t : null;
    private Guid? UserId => Guid.TryParse(Context.User?.FindFirst("userId")?.Value, out var u) ? u : null;

    /// <summary>
    /// Client -> Serveur : rejoindre le groupe d'une voiture spécifique.
    /// Appelé à la connexion initiale ET à chaque reconnexion automatique.
    /// Remet le véhicule en Available sauf si une mission est déjà acceptée/en cours.
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

    // --- Méthodes Serveur -> Client (noms des méthodes attendus côté client) ---
    // MissionProposed(MissionAssignmentResponse)   — nouveau appel vers une voiture
    // VehiclePositionUpdated(VehiclePositionUpdate) — GPS update
    // MissionStatusChanged(MissionResponse)         — changement de statut
    // StreetRiskUpdated(StreetResponse)             — mise à jour risque rue
}
