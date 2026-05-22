namespace PrediCop.Core.DTOs;

public class SetGeofencingRequest
{
    public bool Enabled { get; set; }
}

public class SetDpoEmailRequest
{
    public string? DpoEmail { get; set; }
}

public class TenantSettingsResponse
{
    public bool GeofencingEnabled { get; set; }
    public string? DpoEmail { get; set; }
}
