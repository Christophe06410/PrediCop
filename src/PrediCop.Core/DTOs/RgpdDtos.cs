using PrediCop.Core.Enums;

namespace PrediCop.Core.DTOs;

public record SubmitRgpdRequest(
    string TenantSlug,
    RgpdRequestType RequestType,
    string RequesterName,
    string RequesterEmail,
    string Description
);

public record RgpdRequestResponse(
    Guid Id,
    RgpdRequestType RequestType,
    string RequesterName,
    string RequesterEmail,
    string Description,
    DateTime SubmittedAt,
    bool IsProcessed,
    DateTime? ProcessedAt,
    string? AdminNotes
);

public record ProcessRgpdRequest(
    string? AdminNotes
);
