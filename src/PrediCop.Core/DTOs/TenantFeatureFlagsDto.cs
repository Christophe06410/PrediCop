namespace PrediCop.Core.DTOs;

public record TenantFeatureFlagsResponse(
    bool ModuleRhEnabled,
    bool ModuleFourriereEnabled,
    bool ModuleFleetEnabled,
    bool ModuleLogisticsEnabled,
    bool ModuleVerbalisationEnabled,
    bool AgentBloodTypeEnabled,
    bool AgentEmergencyContactEnabled,
    bool GpsTrackingEnabled,
    bool GeofencingEnabled,
    bool PhotoAttachmentsEnabled,
    int GpsDataRetentionDays,
    int AuditLogRetentionDays
);

public record UpdateModuleFlagsRequest(
    bool ModuleRhEnabled,
    bool ModuleFourriereEnabled,
    bool ModuleFleetEnabled,
    bool ModuleLogisticsEnabled,
    bool ModuleVerbalisationEnabled
);

public record UpdateSensitiveFieldFlagsRequest(
    bool AgentBloodTypeEnabled,
    bool AgentEmergencyContactEnabled,
    bool GpsTrackingEnabled,
    bool GeofencingEnabled,
    bool PhotoAttachmentsEnabled,
    int GpsDataRetentionDays,
    int AuditLogRetentionDays
);
