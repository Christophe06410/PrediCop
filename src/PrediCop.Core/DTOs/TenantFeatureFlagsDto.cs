namespace PrediCop.Core.DTOs;

public record TenantFeatureFlagsResponse(
    bool ModulePlanningEnabled,
    bool ModuleFourriereEnabled,
    bool ModuleFleetEnabled,
    bool ModuleLogisticsEnabled,
    bool ModuleVerbalisationEnabled,
    bool AgentEmergencyContactEnabled,
    bool GpsTrackingEnabled,
    bool GeofencingEnabled,
    bool PhotoAttachmentsEnabled,
    int GpsDataRetentionDays,
    int AuditLogRetentionDays
);

public record UpdateModuleFlagsRequest(
    bool ModulePlanningEnabled,
    bool ModuleFourriereEnabled,
    bool ModuleFleetEnabled,
    bool ModuleLogisticsEnabled,
    bool ModuleVerbalisationEnabled
);

public record UpdateSensitiveFieldFlagsRequest(
    bool AgentEmergencyContactEnabled,
    bool GpsTrackingEnabled,
    bool GeofencingEnabled,
    bool PhotoAttachmentsEnabled,
    int GpsDataRetentionDays,
    int AuditLogRetentionDays
);
