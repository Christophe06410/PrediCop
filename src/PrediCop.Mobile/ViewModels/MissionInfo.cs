namespace PrediCop.Mobile.ViewModels;

public record MissionInfo(
    Guid MissionId,
    Guid? AssignmentId,
    string Reference,
    string Address,
    string Description,
    string Briefing,
    double DistanceKm,
    double Latitude,
    double Longitude
);
