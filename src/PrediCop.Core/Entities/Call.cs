using PrediCop.Core.Enums;

namespace PrediCop.Core.Entities;

public class Call : TenantEntity
{
    public string Reference { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public CallStatus Status { get; set; } = CallStatus.Open;

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
    public User Operator { get; set; } = null!;

    public ICollection<Mission> Missions { get; set; } = [];
}
