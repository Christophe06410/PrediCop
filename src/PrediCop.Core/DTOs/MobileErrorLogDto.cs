namespace PrediCop.Core.DTOs;

/// <summary>Payload envoyé par l'application mobile pour signaler une erreur grave.</summary>
public record MobileErrorLogRequest(
    string AppVersion,
    string Platform,
    string DeviceModel,
    string Category,
    string Message,
    string? StackTrace,
    string? Endpoint,
    int? HttpStatusCode
);
