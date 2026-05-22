namespace PrediCop.Core.DTOs;

public record CreateShiftReportRequest(
    Guid VehicleId,
    DateTime ShiftStart,
    DateTime ShiftEnd,
    string? Notes
);

public record ShiftReportResponse(
    Guid Id,
    Guid VehicleId,
    string VehicleCallSign,
    DateTime ShiftStart,
    DateTime ShiftEnd,
    string OfficerNames,
    int MissionCount,
    int CompletedMissionCount,
    int RefusedMissionCount,
    int PatrolRecordCount,
    double EstimatedKm,
    int DocumentCount,
    string? Notes,
    bool IsSigned,
    DateTime? SignedAt,
    DateTime CreatedAt
);
