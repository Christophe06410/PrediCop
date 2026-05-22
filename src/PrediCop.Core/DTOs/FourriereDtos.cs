using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public record ImpoundedVehicleResponse(
    Guid Id,
    string PlateNumber,
    string Make,
    string Model,
    string Color,
    VehicleCategory Category,
    ImpoundReason Reason,
    DateTime ImpoundedAt,
    Guid AgentId,
    string AgentFullName,
    string AgentBadgeNumber,
    string OriginalAddress,
    string StorageLocation,
    double? Latitude,
    double? Longitude,
    string? ConditionNotes,
    string? PhotoUrls,
    ImpoundStatus Status,
    DateTime? ReleasedAt,
    string? ReleasedToName,
    string? ReleasedToIdNumber,
    DateTime? DestroyedAt,
    string? Notes,
    DateTime CreatedAt
);

public record CreateImpoundRequest(
    string PlateNumber,
    string Make,
    string Model,
    string Color,
    VehicleCategory Category,
    ImpoundReason Reason,
    Guid AgentId,
    string OriginalAddress,
    string StorageLocation,
    double? Latitude,
    double? Longitude,
    string? ConditionNotes,
    string? Notes
);

public record ReleaseVehicleRequest(
    string ReleasedToName,
    string ReleasedToIdNumber,
    string? Notes
);

public record FourriereStatsResponse(
    int TotalInStorage,
    int TotalReleased,
    int TotalDestroyed,
    Dictionary<string, int> ByReason,
    Dictionary<string, int> ByStatus
);
