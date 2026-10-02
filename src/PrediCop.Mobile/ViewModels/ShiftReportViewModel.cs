using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class ShiftReportViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly AuthService _auth;

    public ShiftReportViewModel(ApiService api, AuthService auth)
    {
        _api = api;
        _auth = auth;
        ShiftStartDate = DateTime.Today;
        ShiftStartTime = new TimeSpan(8, 0, 0);
        ShiftEndDate = DateTime.Today;
        ShiftEndTime = TimeSpan.Zero;
        Notes = "";
        ErrorMessage = "";
        ReportVehicle = "";
        ReportOfficers = "";
        ReportMissions = "";
        ReportKm = "";
        ReportDocuments = "";
        ReportSignedInfo = "";
    }

    // --- Formulaire ---
    [ObservableProperty] private DateTime _shiftStartDate;
    [ObservableProperty] private TimeSpan _shiftStartTime;
    [ObservableProperty] private DateTime _shiftEndDate;
    [ObservableProperty] private TimeSpan _shiftEndTime;
    [ObservableProperty] private string _notes;

    // --- États ---
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _isSigning;
    [ObservableProperty] private bool _hasReport;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage;

    // --- Rapport affiché (flat pour éviter les problèmes de binding imbriqué) ---
    [ObservableProperty] private string _reportVehicle;
    [ObservableProperty] private string _reportOfficers;
    [ObservableProperty] private string _reportMissions;
    [ObservableProperty] private string _reportKm;
    [ObservableProperty] private string _reportDocuments;
    [ObservableProperty] private bool _reportIsSigned;
    [ObservableProperty] private string _reportSignedInfo;

    private Guid _reportId;

    public async Task LoadAsync()
    {
        IsLoading = true;
        ShiftEndDate = DateTime.Today;
        ShiftEndTime = DateTime.Now.TimeOfDay;
        ShiftStartDate = DateTime.Today;
        ShiftStartTime = new TimeSpan(8, 0, 0);

        try
        {
            if (_auth.VehicleId.HasValue)
            {
                var vehicle = await _api.GetAsync<VehicleSessionDto>($"api/vehicles/{_auth.VehicleId.Value}");
                if (vehicle?.SessionStartedAt.HasValue == true)
                {
                    var localStart = vehicle.SessionStartedAt.Value.ToLocalTime();
                    ShiftStartDate = localStart.Date;
                    ShiftStartTime = localStart.TimeOfDay;
                }
            }
        }
        catch { }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        var start = ShiftStartDate.Add(ShiftStartTime);
        var end = ShiftEndDate.Add(ShiftEndTime);

        if (end <= start)
        {
            ErrorMessage = "La fin de vacation doit être postérieure au début.";
            HasError = true;
            return;
        }

        IsGenerating = true;
        HasError = false;
        try
        {
            var report = await _api.PostAsync<ShiftReportMobileDto>("api/shift-reports/my", new
            {
                shiftStart = start.ToUniversalTime(),
                shiftEnd = end.ToUniversalTime(),
                notes = Notes
            });

            if (report is null)
            {
                ErrorMessage = "Le serveur n'a pas retourné de rapport.";
                HasError = true;
                return;
            }

            _reportId = report.Id;
            PopulateFromDto(report);
            HasReport = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message.Contains("400") || ex.Message.Contains("Problem")
                ? "Aucun véhicule trouvé pour cette plage. Vérifiez que votre patrouille était bien activée."
                : $"Erreur : {ex.Message}";
            HasError = true;
        }
        finally { IsGenerating = false; }
    }

    [RelayCommand]
    private async Task SignAsync()
    {
        IsSigning = true;
        HasError = false;
        try
        {
            await _api.PostAsync($"api/shift-reports/{_reportId}/sign", null);
            var updated = await _api.GetAsync<ShiftReportMobileDto>($"api/shift-reports/{_reportId}");
            if (updated != null) PopulateFromDto(updated);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de signature : {ex.Message}";
            HasError = true;
        }
        finally { IsSigning = false; }
    }

    [RelayCommand]
    private void Reset()
    {
        HasReport = false;
        HasError = false;
        Notes = "";
        _reportId = Guid.Empty;
    }

    private void PopulateFromDto(ShiftReportMobileDto r)
    {
        ReportVehicle = r.VehicleCallSign;
        ReportOfficers = string.IsNullOrWhiteSpace(r.OfficerNames) ? "—" : r.OfficerNames;
        ReportMissions = $"{r.CompletedMissionCount}/{r.MissionCount} terminée(s), {r.RefusedMissionCount} refusée(s)";
        ReportKm = $"~{r.EstimatedKm:F1} km ({r.PatrolRecordCount} passages)";
        ReportDocuments = r.DocumentCount.ToString();
        ReportIsSigned = r.IsSigned;
        ReportSignedInfo = r.IsSigned && r.SignedAt.HasValue
            ? $"Signé par {r.SignedByName ?? "—"} le {r.SignedAt.Value.ToLocalTime():dd/MM/yyyy à HH:mm}"
            : "";
    }

    private class VehicleSessionDto
    {
        public DateTime? SessionStartedAt { get; set; }
    }
}

public class ShiftReportMobileDto
{
    public Guid Id { get; set; }
    public string VehicleCallSign { get; set; } = "";
    public DateTime ShiftStart { get; set; }
    public DateTime ShiftEnd { get; set; }
    public string OfficerNames { get; set; } = "";
    public int MissionCount { get; set; }
    public int CompletedMissionCount { get; set; }
    public int RefusedMissionCount { get; set; }
    public int PatrolRecordCount { get; set; }
    public double EstimatedKm { get; set; }
    public int DocumentCount { get; set; }
    public bool IsSigned { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignedByName { get; set; }
}
