using PoliceMunicipale.Core.Enums;

namespace PoliceMunicipale.Core.DTOs;

public class CreateCallRequest
{
    public string CallerName { get; set; } = string.Empty;
    public string CallerPhone { get; set; } = string.Empty;
    public string IncidentDescription { get; set; } = string.Empty;
    public string IncidentCategory { get; set; } = string.Empty;
    public string IncidentAddress { get; set; } = string.Empty;
    public string? IncidentAddressComplement { get; set; }
    public double? IncidentLatitude { get; set; }
    public double? IncidentLongitude { get; set; }
    public string? ThirdParties { get; set; }
    public string? Notes { get; set; }
}

public class UpdateCallRequest
{
    public string? CallerName { get; set; }
    public string? CallerPhone { get; set; }
    public string? IncidentDescription { get; set; }
    public string? IncidentCategory { get; set; }
    public string? IncidentAddress { get; set; }
    public string? IncidentAddressComplement { get; set; }
    public double? IncidentLatitude { get; set; }
    public double? IncidentLongitude { get; set; }
    public string? ThirdParties { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
}

public class CallResponse
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public CallStatus Status { get; set; }
    public string CallerName { get; set; } = string.Empty;
    public string CallerPhone { get; set; } = string.Empty;
    public string IncidentDescription { get; set; } = string.Empty;
    public string IncidentCategory { get; set; } = string.Empty;
    public string IncidentAddress { get; set; } = string.Empty;
    public string? IncidentAddressComplement { get; set; }
    public double? IncidentLatitude { get; set; }
    public double? IncidentLongitude { get; set; }
    public string? ThirdParties { get; set; }
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }
    public Guid OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CloseCallRequest
{
    public string? InternalNotes { get; set; }
}
