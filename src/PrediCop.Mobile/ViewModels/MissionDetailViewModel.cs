using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PrediCop.Mobile.ViewModels;

public partial class MissionDetailViewModel : ObservableObject
{
    [ObservableProperty] private string reference = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCallReference))]
    private string callReference = "";
    public bool HasCallReference => !string.IsNullOrEmpty(CallReference);
    [ObservableProperty] private string targetAddress = "";
    [ObservableProperty] private string locationDetail = "";
    [ObservableProperty] private bool hasLocationDetail;
    [ObservableProperty] private string briefingText = "";
    [ObservableProperty] private string narrativeReport = "";
    [ObservableProperty] private bool hasNarrativeReport;
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private Color statusColor = Colors.Gray;
    [ObservableProperty] private string distanceText = "Calcul de la distance...";
    [ObservableProperty] private string createdAtText = "";
    [ObservableProperty] private string dispatchedAtText = "";
    [ObservableProperty] private bool hasDispatchedAt;
    [ObservableProperty] private string arrivedAtText = "";
    [ObservableProperty] private bool hasArrivedAt;
    [ObservableProperty] private string completedAtText = "";
    [ObservableProperty] private bool hasCompletedAt;
    [ObservableProperty] private string completionReport = "";
    [ObservableProperty] private bool hasCompletionReport;
    [ObservableProperty] private bool showAcceptRefuse;
    [ObservableProperty] private bool showComplete;

    // Priorité
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriorityColor))]
    [NotifyPropertyChangedFor(nameof(PriorityEmoji))]
    [NotifyPropertyChangedFor(nameof(HasPriorityBanner))]
    [NotifyPropertyChangedFor(nameof(SosBannerColor))]
    [NotifyPropertyChangedFor(nameof(SosBannerText))]
    private string priority = "Routine";

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

    public bool HasPriorityBanner => Priority is "SOS" or "Critique" or "Urgent";
    public Color SosBannerColor   => Priority is "SOS" or "Critique" ? Colors.Red : Color.FromArgb("#d97706");
    public string SosBannerText   => Priority switch
    {
        "SOS"      => "🚨 MISSION SOS",
        "Critique" => "⚠️ MISSION CRITIQUE",
        "Urgent"   => "❗ MISSION URGENTE",
        _          => ""
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIntervenants))]
    private ObservableCollection<IntervenantVm> intervenants = [];
    public bool HasIntervenants => Intervenants.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAssignments))]
    private ObservableCollection<AssignmentSummaryVm> assignments = [];
    public bool HasAssignments => Assignments.Count > 0;

    [ObservableProperty] private bool isOffline;

    public Guid MissionId { get; set; }
    public Guid? AssignmentId { get; set; }
    public double TargetLat { get; set; }
    public double TargetLng { get; set; }
}

public class AssignmentSummaryVm
{
    public string Summary { get; set; } = "";
    public string Detail { get; set; } = "";
    public Color StatusColor { get; set; } = Colors.Gray;
}

public class IntervenantVm
{
    public string FullName { get; set; } = "";
    public string? Role { get; set; }
    public string? PhoneNumber { get; set; }
    public bool IsInjured { get; set; }
    public string? Notes { get; set; }

    public string Header => IsInjured
        ? $"{FullName} — ⚠ Blessé"
        : FullName;
    public string SubHeader => string.Join("  |  ", new[]
        {
            string.IsNullOrEmpty(Role) ? null : Role,
            string.IsNullOrEmpty(PhoneNumber) ? null : PhoneNumber
        }.Where(s => s != null));
    public bool HasSubHeader => !string.IsNullOrEmpty(SubHeader);
    public bool HasNotes => !string.IsNullOrEmpty(Notes);
}
