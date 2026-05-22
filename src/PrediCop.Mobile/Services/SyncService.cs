using Microsoft.Extensions.Logging;
using PrediCop.Mobile.Models;

namespace PrediCop.Mobile.Services;

public class SyncService
{
    private readonly LocalDbService _localDb;
    private readonly ApiService _api;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<SyncService> _log;

    public SyncService(
        LocalDbService localDb,
        ApiService api,
        IConnectivityService connectivity,
        ILogger<SyncService> log)
    {
        _localDb = localDb;
        _api = api;
        _connectivity = connectivity;
        _log = log;
    }

    /// <summary>
    /// Envoie au serveur toutes les <see cref="PendingTrackingEntry"/> non encore synchronisées.
    /// Les erreurs sont loguées sans lever d'exception afin de ne pas bloquer l'appelant.
    /// </summary>
    public async Task SyncPendingEntriesAsync(CancellationToken ct = default)
    {
        List<PendingTrackingEntry> entries;
        try
        {
            entries = await _localDb.GetUnsyncedEntriesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "SyncPendingEntriesAsync: impossible de lire les entrées locales.");
            return;
        }

        if (entries.Count == 0)
        {
            _log.LogDebug("SyncPendingEntriesAsync: aucune entrée à synchroniser.");
            return;
        }

        _log.LogInformation("SyncPendingEntriesAsync: {Count} entrée(s) à synchroniser.", entries.Count);

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await _api.PostAsync(
                    $"api/missions/{entry.MissionId}/tracking-entries",
                    new
                    {
                        EntryType = entry.EntryType,
                        Content   = entry.Content,
                        CreatedAt = entry.CreatedAt
                    },
                    ct);

                await _localDb.MarkEntrySyncedAsync(entry.LocalId);
                _log.LogDebug("SyncPendingEntriesAsync: entrée {LocalId} synchronisée.", entry.LocalId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "SyncPendingEntriesAsync: impossible de synchroniser l'entrée {LocalId}, on passe à la suivante.",
                    entry.LocalId);
                // On continue sans lever d'exception
            }
        }
    }

    /// <summary>
    /// S'abonne aux changements de connectivité et déclenche une synchronisation
    /// automatique dès que l'accès Internet est rétabli.
    /// </summary>
    public void StartAutoSync()
    {
        _connectivity.ConnectivityChanged += async (_, isConnected) =>
        {
            if (!isConnected) return;

            _log.LogInformation("Connectivité rétablie — démarrage de la synchronisation automatique.");
            await SyncPendingEntriesAsync();
        };
    }
}
