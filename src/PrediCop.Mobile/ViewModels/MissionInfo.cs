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
    double Longitude,
    string Priority = "Routine"
)
{
    public Color PriorityColor => Priority switch
    {
        "SOS"      => Colors.Red,
        "Critique" => Color.FromArgb("#dc2626"),
        "Urgent"   => Color.FromArgb("#f59e0b"),
        _          => Colors.Gray
    };

    public string PriorityEmoji => Priority switch
    {
        "SOS"      => "🚨",
        "Critique" => "⚠️",
        "Urgent"   => "❗",
        _          => ""
    };

    public bool IsSosOrCritique => Priority is "SOS" or "Critique";
    public Color SosBannerColor => Priority is "SOS" or "Critique" ? Colors.Red : Color.FromArgb("#1e3a5f");
    public string SosBannerText => Priority switch
    {
        "SOS"      => "🚨 MISSION SOS",
        "Critique" => "⚠️ MISSION CRITIQUE",
        "Urgent"   => "❗ MISSION URGENTE",
        _          => ""
    };
    public bool HasPriorityBanner => Priority is "SOS" or "Critique" or "Urgent";
};
