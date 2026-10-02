using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PrediCop.Mobile.ViewModels;

public partial class MissionDetailViewModel : ObservableObject
{
    public MissionDetailViewModel()
    {
        Reference = "";
        CallReference = "";
        TargetAddress = "";
        LocationDetail = "";
        BriefingText = "";
        NarrativeReport = "";
        StatusText = "";
        StatusColor = Colors.Gray;
        DistanceText = "Calcul de la distance...";
        CreatedAtText = "";
        DispatchedAtText = "";
        ArrivedAtText = "";
        CompletedAtText = "";
        CompletionReport = "";
        Priority = "Routine";
        Intervenants = [];
        Assignments = [];
        UploadStatus = "";
        PhotoStatus = "";
        CallerName = "";
        CallerPhone = "";
        IncidentCategory = "";
        IncidentAddressComplement = "";
        CallNotes = "";
        ThirdParties = "";
    }

    [ObservableProperty] private string _reference;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCallReference))]
    private string _callReference;
    public bool HasCallReference => !string.IsNullOrEmpty(CallReference);
    [ObservableProperty] private string _targetAddress;
    [ObservableProperty] private string _locationDetail;
    [ObservableProperty] private bool _hasLocationDetail;
    [ObservableProperty] private string _briefingText;
    [ObservableProperty] private string _narrativeReport;
    [ObservableProperty] private bool _hasNarrativeReport;
    [ObservableProperty] private string _statusText;
    [ObservableProperty] private Color _statusColor;
    [ObservableProperty] private string _distanceText;
    [ObservableProperty] private string _createdAtText;
    [ObservableProperty] private string _dispatchedAtText;
    [ObservableProperty] private bool _hasDispatchedAt;
    [ObservableProperty] private string _arrivedAtText;
    [ObservableProperty] private bool _hasArrivedAt;
    [ObservableProperty] private string _completedAtText;
    [ObservableProperty] private bool _hasCompletedAt;
    [ObservableProperty] private string _completionReport;
    [ObservableProperty] private bool _hasCompletionReport;
    [ObservableProperty] private bool _showAcceptRefuse;
    [ObservableProperty] private bool _showComplete;

    // Priorité
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PriorityColor))]
    [NotifyPropertyChangedFor(nameof(PriorityEmoji))]
    [NotifyPropertyChangedFor(nameof(HasPriorityBanner))]
    [NotifyPropertyChangedFor(nameof(SosBannerColor))]
    [NotifyPropertyChangedFor(nameof(SosBannerText))]
    private string _priority;

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
    private ObservableCollection<IntervenantVm> _intervenants;
    public bool HasIntervenants => Intervenants.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAssignments))]
    private ObservableCollection<AssignmentSummaryVm> _assignments;
    public bool HasAssignments => Assignments.Count > 0;

    // Upload médias
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotUploading))]
    private bool _isUploading;
    [ObservableProperty] private double _uploadProgress;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUploadStatus))]
    private string _uploadStatus;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhotoStatus))]
    private string _photoStatus;
    public bool IsNotUploading => !IsUploading;
    public bool HasUploadStatus => !string.IsNullOrEmpty(UploadStatus);
    public bool HasPhotoStatus => !string.IsNullOrEmpty(PhotoStatus);

    [ObservableProperty] private bool _isOffline;
    [ObservableProperty] private bool _showEditReport;

    // Données de l'appel source (main courante)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCallDetails))]
    private string _callerName;
    [ObservableProperty] private string _callerPhone;
    [ObservableProperty] private string _incidentCategory;
    [ObservableProperty] private string _incidentAddressComplement;
    [ObservableProperty] private string _callNotes;
    [ObservableProperty] private string _thirdParties;

    public bool HasCallDetails => !string.IsNullOrEmpty(CallerName)
        || !string.IsNullOrEmpty(CallerPhone)
        || !string.IsNullOrEmpty(IncidentCategory)
        || !string.IsNullOrEmpty(IncidentAddressComplement)
        || !string.IsNullOrEmpty(CallNotes)
        || !string.IsNullOrEmpty(ThirdParties);

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
