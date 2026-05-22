using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class TicketingViewModel(
    ApiService api,
    AuthService auth,
    ILogger<TicketingViewModel> log) : ObservableObject
{
    // ── UI state ──────────────────────────────────────────────────────────────

    [ObservableProperty] private bool showForm;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSubmitting))]
    private bool isSubmitting;
    public bool IsNotSubmitting => !IsSubmitting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoadingLocation))]
    private bool isLoadingLocation;
    public bool IsNotLoadingLocation => !IsLoadingLocation;
    [ObservableProperty] private bool isLoadingHistory;

    [ObservableProperty] private string statusMessage = "";
    [ObservableProperty] private bool hasStatusMessage;
    [ObservableProperty] private bool isStatusError;

    // ── Form fields ───────────────────────────────────────────────────────────

    [ObservableProperty] private string plateNumber = "";
    [ObservableProperty] private string address = "";
    [ObservableProperty] private string fineAmountText = "";
    [ObservableProperty] private string vehicleMake = "";
    [ObservableProperty] private string vehicleModel = "";
    [ObservableProperty] private string vehicleColor = "";
    [ObservableProperty] private string notes = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FineAmountText))]
    private InfractionItem? selectedInfraction;

    partial void OnSelectedInfractionChanged(InfractionItem? value)
    {
        if (value is not null)
            FineAmountText = value.DefaultFine.ToString("F2");
    }

    // ── History ───────────────────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<TicketSummary> recentTickets = [];
    [ObservableProperty] private bool hasNoTickets;

    public static List<InfractionItem> InfractionTypes { get; } =
    [
        new("StationnementInterdit",    "Stationnement interdit",           35m),
        new("StationnementGenant",      "Stationnement gênant",             35m),
        new("StationnementDangereux",   "Stationnement dangereux",         135m),
        new("StationnementHandicape",   "Stat. emplacement handicapé",     135m),
        new("VitesseExcessive",         "Excès de vitesse",                135m),
        new("FeuRouge",                 "Non-respect feu rouge",           135m),
        new("NonRespectPriorite",       "Non-respect priorité",            135m),
        new("PortableAuVolant",         "Téléphone au volant",             135m),
        new("CeintureSecurity",         "Ceinture non attachée",           135m),
        new("DefautAssurance",          "Défaut d'assurance",              500m),
        new("DefautControleTechnique",  "Défaut contrôle technique",       135m),
        new("NuisanceSonore",           "Nuisance sonore",                  68m),
        new("DegradationEspacePublic",  "Dégradation espace public",        68m),
        new("Autre",                    "Autre infraction",                   0m),
    ];

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleForm()
    {
        ShowForm = !ShowForm;
        if (ShowForm) ClearForm();
    }

    [RelayCommand]
    private async Task UseMyLocationAsync()
    {
        IsLoadingLocation = true;
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync()
                           ?? await Geolocation.GetLocationAsync(
                               new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
            if (location is null) return;

            var placemarks = await Geocoding.GetPlacemarksAsync(location.Latitude, location.Longitude);
            var p = placemarks?.FirstOrDefault();
            if (p is not null)
            {
                var parts = new[] { $"{p.SubThoroughfare} {p.Thoroughfare}".Trim(), p.Locality }
                    .Where(s => !string.IsNullOrWhiteSpace(s));
                Address = string.Join(", ", parts);
            }
            else
                Address = $"{location.Latitude:F5}, {location.Longitude:F5}";
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Impossible de récupérer la position GPS.");
        }
        finally { IsLoadingLocation = false; }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(PlateNumber))
        { SetStatus("Numéro de plaque requis.", isError: true); return; }

        if (SelectedInfraction is null)
        { SetStatus("Sélectionnez un type d'infraction.", isError: true); return; }

        if (string.IsNullOrWhiteSpace(Address))
        { SetStatus("Adresse requise.", isError: true); return; }

        if (!decimal.TryParse(FineAmountText.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var fine) || fine < 0)
        { SetStatus("Montant invalide.", isError: true); return; }

        IsSubmitting = true;
        HasStatusMessage = false;
        try
        {
            var body = new
            {
                IssuedById   = auth.CurrentUser!.Id,
                IssuedAtAddress = Address.Trim(),
                PlateNumber  = PlateNumber.Trim().ToUpperInvariant(),
                VehicleMake  = VehicleMake.Trim(),
                VehicleModel = VehicleModel.Trim(),
                VehicleColor = VehicleColor.Trim(),
                InfractionType = SelectedInfraction.EnumKey,
                FineAmount   = fine,
                Notes        = Notes.Trim()
            };

            await api.PostAsync("/api/tickets", body);

            SetStatus($"PV émis — {PlateNumber.Trim().ToUpperInvariant()}", isError: false);
            ClearForm();
            ShowForm = false;
            await LoadRecentAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Erreur lors de la création du PV.");
            SetStatus("Erreur lors de l'émission du PV.", isError: true);
        }
        finally { IsSubmitting = false; }
    }

    [RelayCommand]
    public async Task LoadRecentAsync()
    {
        IsLoadingHistory = true;
        RecentTickets.Clear();
        HasNoTickets = false;
        try
        {
            var from = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
            var agentId = auth.CurrentUser?.Id;
            var url = $"/api/tickets?dateFrom={from}&agentId={agentId}";

            var list = await api.GetAsync<List<TicketDto>>(url);
            if (list is null || list.Count == 0) { HasNoTickets = true; return; }

            foreach (var t in list)
                RecentTickets.Add(new TicketSummary(
                    t.TicketNumber,
                    t.PlateNumber,
                    GetInfractionLabel(t.InfractionType),
                    t.FineAmount,
                    GetStatusLabel(t.Status),
                    GetStatusColor(t.Status),
                    t.IssuedAt.ToLocalTime().ToString("dd/MM HH:mm")));
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Impossible de charger les PV récents.");
            HasNoTickets = true;
        }
        finally { IsLoadingHistory = false; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ClearForm()
    {
        PlateNumber = VehicleMake = VehicleModel = VehicleColor = Notes = Address = FineAmountText = "";
        SelectedInfraction = null;
    }

    private void SetStatus(string msg, bool isError)
    {
        StatusMessage  = msg;
        IsStatusError  = isError;
        HasStatusMessage = true;
    }

    private static string GetInfractionLabel(string key) =>
        InfractionTypes.FirstOrDefault(i => i.EnumKey == key)?.Label ?? key;

    private static string GetStatusLabel(string status) => status switch
    {
        "Issued"    => "Émis",
        "Paid"      => "Payé",
        "Contested" => "Contesté",
        "Cancelled" => "Annulé",
        _           => status
    };

    private static Color GetStatusColor(string status) => status switch
    {
        "Issued"    => Color.FromArgb("#2563eb"),
        "Paid"      => Color.FromArgb("#16a34a"),
        "Contested" => Color.FromArgb("#d97706"),
        "Cancelled" => Color.FromArgb("#6b7280"),
        _           => Color.FromArgb("#6b7280")
    };

    // ── Local DTOs ────────────────────────────────────────────────────────────

    private class TicketDto
    {
        public string TicketNumber   { get; set; } = "";
        public string PlateNumber    { get; set; } = "";
        public string InfractionType { get; set; } = "";
        public decimal FineAmount    { get; set; }
        public string Status         { get; set; } = "";
        public DateTime IssuedAt     { get; set; }
    }
}

public record InfractionItem(string EnumKey, string Label, decimal DefaultFine)
{
    public override string ToString() => Label;
}

public record TicketSummary(
    string Number,
    string Plate,
    string Infraction,
    decimal Amount,
    string StatusLabel,
    Color StatusColor,
    string FormattedDate);
