using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class ShiftReportViewModel(ApiService api, AuthService auth) : ObservableObject
{
    // --- Formulaire ---
    [ObservableProperty] private DateTime shiftStartDate = DateTime.Today;
    [ObservableProperty] private TimeSpan shiftStartTime = new TimeSpan(8, 0, 0);
    [ObservableProperty] private DateTime shiftEndDate = DateTime.Today;
    [ObservableProperty] private TimeSpan shiftEndTime = TimeSpan.Zero;
    [ObservableProperty] private string notes = "";

    // --- États ---
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isGenerating;
    [ObservableProperty] private bool isSigning;
    [ObservableProperty] private bool hasReport;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private string errorMessage = "";

    // --- Rapport affiché (flat pour éviter les problèmes de binding imbriqué) ---
    [ObservableProperty] private string reportVehicle = "";
    [ObservableProperty] private string reportOfficers = "";
    [ObservableProperty] private string reportMissions = "";
    [ObservableProperty] private string reportKm = "";
    [ObservableProperty] private string reportDocuments = "";
    [ObservableProperty] private bool reportIsSigned;
    [ObservableProperty] private string reportSignedInfo = "";

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
            if (auth.VehicleId.HasValue)
            {
                var vehicle = await api.GetAsync<VehicleSessionDto>($"api/vehicles/{auth.VehicleId.Value}");
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
            var report = await api.PostAsync<ShiftReportMobileDto>("api/shift-reports/my", new
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
            await api.PostAsync($"api/shift-reports/{_reportId}/sign", null);
            var updated = await api.GetAsync<ShiftReportMobileDto>($"api/shift-reports/{_reportId}");
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
