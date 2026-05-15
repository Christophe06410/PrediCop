using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PoliceMunicipale.Api.Hubs;

[Authorize]
public class PoliceHub : Hub
{
    /// <summary>
    /// Client -> Serveur : rejoindre le groupe d'une voiture spécifique.
    /// </summary>
    public async Task JoinVehicleGroup(string vehicleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vehicle_{vehicleId}");
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
        }
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenantId")?.Value;
        if (tenantId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant_{tenantId}");

        await base.OnConnectedAsync();
    }

    // --- Méthodes Serveur -> Client (noms des méthodes attendus côté client) ---
    // MissionProposed(MissionAssignmentResponse)   — nouveau appel vers une voiture
    // VehiclePositionUpdated(VehiclePositionUpdate) — GPS update
    // MissionStatusChanged(MissionResponse)         — changement de statut
    // StreetRiskUpdated(StreetResponse)             — mise à jour risque rue
}
