using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public record AgentProfileResponse(
    Guid Id,
    Guid AgentId,
    string AgentFullName,
    string AgentBadgeNumber,
    string? EmergencyContact1Name,
    string? EmergencyContact1Phone,
    string? EmergencyContact1Relationship,
    string? EmergencyContact2Name,
    string? EmergencyContact2Phone,
    string? Notes);

public record UpsertAgentProfileRequest(
    string? EmergencyContact1Name,
    string? EmergencyContact1Phone,
    string? EmergencyContact1Relationship,
    string? EmergencyContact2Name,
    string? EmergencyContact2Phone,
    string? Notes);

public record LeaveResponse(
    Guid Id,
    Guid AgentId,
    string AgentFullName,
    string AgentBadgeNumber,
    LeaveType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    LeaveStatus Status,
    DateTime RequestedAt,
    DateTime? ApprovedAt,
    string? ApprovedByName,
    string? Notes,
    string? RejectionReason);

public record CreateLeaveRequest(
    Guid AgentId,
    LeaveType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes);

public record ApproveLeaveRequest(string? Notes);

public record RejectLeaveRequest(string RejectionReason);

public record ShiftScheduleResponse(
    Guid Id,
    Guid AgentId,
    string AgentFullName,
    string AgentBadgeNumber,
    Guid? VehicleId,
    string? VehicleCallSign,
    DateOnly Date,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    bool IsPublished,
    string? Notes);

public record UpsertShiftRequest(
    Guid AgentId,
    Guid? VehicleId,
    DateOnly Date,
    TimeOnly ShiftStart,
    TimeOnly ShiftEnd,
    bool IsPublished,
    string? Notes);

public record VehicleOccupancyResponse(
    Guid VehicleId,
    string CallSign,
    int Capacity,
    int Count,
    List<string> AgentNames);

public record LeaveEntitlementResponse(
    Guid Id,
    Guid AgentId,
    string AgentFullName,
    LeaveType Type,
    decimal TotalDays,
    decimal UsedDays,
    decimal RemainingDays,
    DateOnly ValidFrom,
    DateOnly ValidTo);

public record CreateLeaveEntitlementRequest(
    Guid AgentId,
    LeaveType Type,
    decimal TotalDays,
    DateOnly ValidFrom,
    DateOnly ValidTo);

public record UpdateLeaveEntitlementRequest(
    decimal TotalDays,
    DateOnly ValidFrom,
    DateOnly ValidTo);

public record AgentLeaveBalanceResponse(
    LeaveType Type,
    decimal TotalDays,
    decimal UsedDays,
    decimal RemainingDays,
    DateOnly ValidFrom,
    DateOnly ValidTo);

// -------- CSV Import --------

public record CsvImportResult(
    int Imported,
    int Skipped,
    List<CsvImportError> Errors);

public record CsvImportError(
    int LineNumber,
    string RawLine,
    string Message);
