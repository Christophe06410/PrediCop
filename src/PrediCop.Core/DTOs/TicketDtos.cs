using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public record ElectronicTicketResponse(
    Guid Id,
    string TicketNumber,
    DateTime IssuedAt,
    Guid IssuedById,
    string IssuedByFullName,
    string IssuedByBadgeNumber,
    string IssuedAtAddress,
    double? Latitude,
    double? Longitude,
    string PlateNumber,
    string? VehicleMake,
    string? VehicleModel,
    string? VehicleColor,
    InfractionType InfractionType,
    string? ArticleCode,
    decimal FineAmount,
    string? Notes,
    TicketStatus Status,
    bool IsSigned,
    DateTime? SignedAt,
    bool ExportedToAntai,
    DateTime? ExportedAt,
    DateTime CreatedAt,
    Guid? MissionId,
    string? MissionReference
);

public record CreateTicketRequest(
    Guid IssuedById,
    string IssuedAtAddress,
    double? Latitude,
    double? Longitude,
    string PlateNumber,
    string? VehicleMake,
    string? VehicleModel,
    string? VehicleColor,
    InfractionType InfractionType,
    string? ArticleCode,
    decimal FineAmount,
    string? Notes,
    Guid? MissionId = null
);

public record UpdateTicketStatusRequest(
    TicketStatus Status,
    string? Notes
);

public record TicketStatsResponse(
    int TotalIssued,
    int TotalPaid,
    int TotalContested,
    int TotalCancelled,
    decimal TotalFineAmount,
    Dictionary<string, int> ByInfractionType,
    Dictionary<string, int> ByAgent,
    Dictionary<string, int> ByDayOfWeek
);
