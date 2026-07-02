namespace PrediCop.Core.DTOs;

/// <summary>Payload de log debug mobile (DEBUG uniquement, pas persisté en DB).</summary>
public record MobileDebugLogRequest(string Tag, string Message);

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
