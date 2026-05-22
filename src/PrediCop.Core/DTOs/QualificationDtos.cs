using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public record CreateQualificationRequest(
    Guid AgentId,
    QualificationType Type,
    string Reference,
    string IssuingAuthority,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    string? Notes
);

public record UpdateQualificationRequest(
    QualificationType Type,
    string Reference,
    string IssuingAuthority,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    string? Notes
);

public record QualificationResponse(
    Guid Id,
    Guid AgentId,
    string AgentFullName,
    string AgentBadgeNumber,
    QualificationType Type,
    string Reference,
    string IssuingAuthority,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    string? Notes,
    bool IsExpired,
    bool ExpiresWithin30Days
);
