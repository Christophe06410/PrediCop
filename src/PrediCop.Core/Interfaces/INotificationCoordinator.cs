namespace PrediCop.Core.Interfaces;

public interface INotificationCoordinator
{
    /// <summary>
    /// Envoie l'événement MissionProposed via SignalR et démarre un timer de 15s.
    /// Si aucun ack n'est reçu dans ce délai, envoie un push Firebase en fallback.
    /// </summary>
    Task NotifyMissionProposedAsync(
        Guid assignmentId,
        Guid vehicleId,
        string firebaseTitle,
        string firebaseBody,
        CancellationToken ct = default);

    /// <summary>
    /// Acquittement reçu du mobile — annule le timer Firebase pour cet assignment.
    /// </summary>
    void Ack(Guid assignmentId);
}
