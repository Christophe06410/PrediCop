using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public record VehicleLogEntryResponse(
    Guid Id,
    Guid VehicleId,
    string VehicleCallSign,
    string VehiclePlate,
    Guid OfficerId,
    string OfficerFullName,
    DateOnly Date,
    int KmStart,
    int KmEnd,
    int KmTotal,
    decimal? FuelAdded,
    string? Destination,
    string? Notes,
    DateTime CreatedAt);

public record CreateLogEntryRequest(
    Guid VehicleId,
    Guid OfficerId,
    DateOnly Date,
    int KmStart,
    int KmEnd,
    decimal? FuelAdded,
    string? Destination,
    string? Notes);

public record VehicleMaintenanceResponse(
    Guid Id,
    Guid VehicleId,
    string VehicleCallSign,
    string VehiclePlate,
    MaintenanceType Type,
    DateTime ScheduledDate,
    DateTime? CompletedAt,
    int? KmAtService,
    string Description,
    decimal? Cost,
    string? ProviderName,
    string? Notes,
    bool IsCompleted,
    bool IsOverdue,
    bool IsUpcoming);

public record CreateMaintenanceRequest(
    Guid VehicleId,
    MaintenanceType Type,
    DateTime ScheduledDate,
    string Description,
    int? KmAtService,
    decimal? Cost,
    string? ProviderName,
    string? Notes);

public record CompleteMaintenanceRequest(
    DateTime CompletedAt,
    int? KmAtService,
    decimal? Cost,
    string? ProviderName,
    string? Notes);

public record FleetAlertResponse(
    Guid VehicleId,
    string VehicleCallSign,
    string VehiclePlate,
    string AlertType,
    string Description,
    DateTime DueDate);

public record VehicleSummaryResponse(
    Guid Id,
    string CallSign,
    string LicensePlate,
    int TotalKmThisMonth,
    int TotalTrips,
    DateTime? NextMaintenanceDate,
    string? NextMaintenanceType,
    bool HasOverdueMaintenance);
