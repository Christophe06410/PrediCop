using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PrediCop.Mobile.ViewModels;

public partial class MissionDetailViewModel : ObservableObject
{
    [ObservableProperty] private string reference = "";
    [ObservableProperty] private string targetAddress = "";
    [ObservableProperty] private string briefingText = "";
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private Color statusColor = Colors.Gray;
    [ObservableProperty] private string distanceText = "Calcul de la distance...";
    [ObservableProperty] private string createdAtText = "";
    [ObservableProperty] private bool showAcceptRefuse;
    [ObservableProperty] private bool showComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAssignments))]
    private ObservableCollection<AssignmentSummaryVm> assignments = [];
    public bool HasAssignments => Assignments.Count > 0;

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
