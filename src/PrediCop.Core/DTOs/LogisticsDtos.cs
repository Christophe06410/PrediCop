using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public record EquipmentCatalogResponse(
    Guid Id,
    string Name,
    EquipmentCategory Category,
    string? Description,
    string Unit,
    int? DefaultLifespanMonths,
    string? ReferenceCode,
    bool IsActive);

public record CreateEquipmentRequest(
    string Name,
    EquipmentCategory Category,
    string? Description,
    string Unit,
    int? DefaultLifespanMonths,
    string? ReferenceCode);

public record UpdateEquipmentRequest(
    string Name,
    EquipmentCategory Category,
    string? Description,
    string Unit,
    int? DefaultLifespanMonths,
    string? ReferenceCode,
    bool IsActive);

public record EquipmentIssuanceResponse(
    Guid Id,
    Guid AgentId,
    string AgentFullName,
    string AgentBadgeNumber,
    Guid EquipmentCatalogId,
    string EquipmentName,
    EquipmentCategory EquipmentCategory,
    int Quantity,
    string Unit,
    DateTime IssuedAt,
    DateTime? ExpiresAt,
    string? Size,
    string? SerialNumber,
    string? Notes,
    bool IsReturned,
    DateTime? ReturnedAt,
    bool IsExpired,
    bool ExpiresWithin60Days);

public record CreateIssuanceRequest(
    Guid AgentId,
    Guid EquipmentCatalogId,
    int Quantity,
    DateTime? ExpiresAt,
    string? Size,
    string? SerialNumber,
    string? Notes);

public record UniformProfileResponse(
    Guid Id,
    Guid AgentId,
    string AgentFullName,
    string AgentBadgeNumber,
    string? JacketSize,
    string? PantSize,
    string? ShirtSize,
    string? ShoeSize,
    string? HatSize,
    string? Notes);

public record UpsertUniformProfileRequest(
    string? JacketSize,
    string? PantSize,
    string? ShirtSize,
    string? ShoeSize,
    string? HatSize,
    string? Notes);

public record LogisticsAlertResponse(
    Guid IssuanceId,
    Guid AgentId,
    string AgentFullName,
    string EquipmentName,
    DateTime ExpiresAt,
    bool IsExpired);
