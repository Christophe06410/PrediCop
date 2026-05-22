namespace PrediCop.Mobile.Messages;

/// <summary>Message envoyé au ViewModel de la page pour demander une confirmation SOS.</summary>
public record SosConfirmationRequest(Guid VehicleId);
