using Microsoft.AspNetCore.SignalR.Client;

namespace PrediCop.Mobile.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;
    private string? _token;
    private Guid _vehicleId;
    private bool _manualStop;

    public event EventHandler<MissionProposedArgs>? MissionProposed;
    public event EventHandler<string>? MissionStatusChanged;
    public event EventHandler<StreetRiskArgs>? StreetRiskUpdated;
    public event EventHandler? Reconnected;

    /// <summary>Notifie chaque changement d'état de la connexion temps réel (pour l'UI).</summary>
    public event EventHandler<SignalRStatus>? StatusChanged;

    private SignalRStatus _status = SignalRStatus.Disconnected;
    public SignalRStatus Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            Log($"État → {value}");
            StatusChanged?.Invoke(this, value);
        }
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public SignalRService(string baseUrl)
    {
        _hubUrl = $"{baseUrl}/hubs/police";
    }

    private static void Log(string message)
    {
#if DEBUG
        MobileLogger.Log("SignalR", message);
#endif
        System.Diagnostics.Debug.WriteLine($"[SignalR] {message}");
    }

    public async Task ConnectAsync(string token, Guid vehicleId)
    {
        _token = token;
        _vehicleId = vehicleId;
        _manualStop = false;

        // Rebuild propre si une connexion existait déjà (re-login, changement de véhicule).
        if (_connection is not null)
        {
            try { await _connection.DisposeAsync(); } catch { }
            _connection = null;
        }

        Log($"ConnectAsync — hub={_hubUrl}, vehicleId={vehicleId}");
        Status = SignalRStatus.Connecting;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(_token);
#if DEBUG
                // En DEBUG l'API dev sert un certificat auto-signé (localhost / IP LAN).
                // Le HttpClient REST le contourne déjà ; SignalR doit faire parein sinon
                // le handshake TLS échoue en silence (WebSocket + long-polling) et le
                // mobile ne reçoit JAMAIS les events, alors que le REST fonctionne.
                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is HttpClientHandler h)
                        h.ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    return handler;
                };
                options.WebSocketConfiguration = ws =>
                    ws.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#endif
            })
            // Politique de reconnexion INFINIE : le défaut abandonne après ~30s,
            // ce qui laissait le mobile hors du groupe vehicle_{id} après une coupure wifi.
            .WithAutomaticReconnect(new InfiniteRetryPolicy())
            .Build();

        _connection.On<System.Text.Json.JsonElement>("MissionProposed", data =>
        {
            Log("Event reçu : MissionProposed");
            var assignmentId = data.TryGetProperty("assignmentId", out var el)
                ? el.GetGuid()
                : Guid.Empty;
            MissionProposed?.Invoke(this, new MissionProposedArgs(assignmentId));
        });

        _connection.On<object>("MissionStatusChanged", data =>
        {
            Log("Event reçu : MissionStatusChanged");
            MissionStatusChanged?.Invoke(this, data?.ToString() ?? "");
        });

        _connection.On<object>("StreetRiskUpdated", data =>
            StreetRiskUpdated?.Invoke(this, new StreetRiskArgs(data)));

        _connection.Reconnecting += error =>
        {
            Log($"Reconnecting… ({error?.Message ?? "raison inconnue"})");
            Status = SignalRStatus.Reconnecting;
            return Task.CompletedTask;
        };

        // Après un auto-reconnect, la connexion a un nouveau ConnectionId et n'est plus
        // dans le groupe véhicule — on le rejoint explicitement puis on rafraîchit l'état.
        _connection.Reconnected += async connectionId =>
        {
            Log($"Reconnected (connId={connectionId}) — rejoint vehicle_{_vehicleId}");
            try { await _connection!.InvokeAsync("JoinVehicleGroup", _vehicleId); }
            catch (Exception ex) { Log($"JoinVehicleGroup post-reconnect ÉCHEC : {ex.Message}"); }
            Reconnected?.Invoke(this, EventArgs.Empty);
        };

        // Filet de sécurité : si la connexion se ferme malgré la politique infinie
        // (ex. échec du tout premier StartAsync suivi d'une fermeture), on relance en boucle.
        _connection.Closed += async error =>
        {
            Log($"Closed ({error?.Message ?? "fermeture propre"}). manualStop={_manualStop}");
            Status = SignalRStatus.Disconnected;
            if (_manualStop) return;
            await Task.Delay(TimeSpan.FromSeconds(5));
            await RestartLoopAsync();
        };

        try
        {
            await StartAndJoinAsync();
        }
        catch (Exception ex)
        {
            // Ne pas laisser l'exception filer en silence (le caller fait souvent ContinueWith(_=>{})).
            Log($"Échec connexion initiale : {ex.GetType().Name} — {ex.Message}");
            _ = RestartLoopAsync(); // retente en arrière-plan sans bloquer le login
        }
    }

    private async Task StartAndJoinAsync()
    {
        await _connection!.StartAsync();
        Log($"Connecté (connId={_connection.ConnectionId}) — rejoint vehicle_{_vehicleId}");
        await _connection.InvokeAsync("JoinVehicleGroup", _vehicleId);
        Status = SignalRStatus.Connected;
        // Rattrape les messages éventuellement manqués pendant la coupure.
        Reconnected?.Invoke(this, EventArgs.Empty);
    }

    private async Task RestartLoopAsync()
    {
        while (!_manualStop && _connection is { State: HubConnectionState.Disconnected })
        {
            try
            {
                Log("Reconnexion manuelle…");
                Status = SignalRStatus.Connecting;
                await StartAndJoinAsync();
                Log("Reconnexion manuelle réussie.");
                return;
            }
            catch (Exception ex)
            {
                Log($"Reconnexion manuelle échouée : {ex.Message}. Nouvel essai dans 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    /// <summary>
    /// Envoie un ack au serveur pour annuler le timer Firebase de 15s.
    /// Fire-and-forget sûr : les exceptions sont swallowées.
    /// </summary>
    public async Task AckMissionNotificationAsync(Guid assignmentId)
    {
        try
        {
            if (_connection?.State == HubConnectionState.Connected && assignmentId != Guid.Empty)
                await _connection.InvokeAsync("AckMissionNotification", assignmentId);
        }
        catch (Exception ex)
        {
            Log($"AckMissionNotification échec : {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _manualStop = true;
        Status = SignalRStatus.Disconnected;
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}

/// <summary>État de la connexion temps réel (SignalR), indépendant de l'accès réseau REST.</summary>
public enum SignalRStatus { Disconnected, Connecting, Reconnecting, Connected }

public record MissionProposedArgs(Guid AssignmentId);
public record StreetRiskArgs(object Data);

/// <summary>Reconnexion sans limite de temps (le défaut SignalR abandonne après ~30s).</summary>
internal sealed class InfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        retryContext.PreviousRetryCount switch
        {
            0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(5),
            3 => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(15)
        };
}
