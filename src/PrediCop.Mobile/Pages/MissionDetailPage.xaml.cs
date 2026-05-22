using System.Collections.ObjectModel;
using System.Text.Json;
using PrediCop.Mobile.Models;
using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class MissionDetailPage : ContentPage
{
    private readonly ApiService _api;
    private readonly LocalDbService _localDb;
    private readonly IConnectivityService _connectivity;
    private readonly SyncService _syncService;
    private readonly MissionDetailViewModel _vm;
    private readonly Guid _missionId;

    public MissionDetailPage(Guid missionId, ApiService api, LocalDbService localDb,
        IConnectivityService connectivity, SyncService syncService)
    {
        InitializeComponent();
        _missionId = missionId;
        _api = api;
        _localDb = localDb;
        _connectivity = connectivity;
        _syncService = syncService;
        _vm = new MissionDetailViewModel { MissionId = missionId };
        BindingContext = _vm;

        // Mettre à jour le bandeau hors-ligne en temps réel
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.IsOffline = !_connectivity.IsConnected;
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, bool isConnected)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            _vm.IsOffline = !isConnected;
            if (isConnected)
            {
                // Synchroniser les entrées en attente puis recharger
                await _syncService.SyncPendingEntriesAsync();
                await LoadAsync();
            }
        });
    }

    private async Task LoadAsync(CancellationToken ct = default)
    {
        if (_connectivity.IsConnected)
        {
            await LoadFromApiAsync(ct);
        }
        else
        {
            await LoadFromCacheAsync();
        }
    }

    private async Task LoadFromApiAsync(CancellationToken ct = default)
    {
        try
        {
            var mission = await _api.GetAsync<MissionDetailDto>($"api/missions/{_missionId}", ct);
            if (mission == null) return;

            // Mise en cache pour usage hors-ligne
            var rawJson = JsonSerializer.Serialize(mission);
            await _localDb.UpsertCachedMissionAsync(new CachedMission
            {
                Id           = mission.Id,
                Reference    = mission.Reference,
                TargetAddress = mission.TargetAddress,
                Status       = mission.Status,
                BriefingText = mission.BriefingText,
                CachedAt     = DateTime.UtcNow,
                RawJson      = rawJson
            });

            ApplyMissionToViewModel(mission);
            await ComputeDistanceAsync();
        }
        catch
        {
            await DisplayAlert("Erreur", "Impossible de charger les détails de la mission.", "OK");
        }
    }

    private async Task LoadFromCacheAsync()
    {
        var cached = await _localDb.GetCachedMissionAsync(_missionId);
        if (cached == null)
        {
            await DisplayAlert("Hors ligne", "Mission non disponible hors connexion.", "OK");
            return;
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var mission = JsonSerializer.Deserialize<MissionDetailDto>(cached.RawJson, options);
            if (mission == null) return;
            ApplyMissionToViewModel(mission);
        }
        catch
        {
            // Fallback minimal si le JSON est corrompu
            _vm.Reference     = cached.Reference;
            _vm.TargetAddress = cached.TargetAddress;
            _vm.BriefingText  = cached.BriefingText ?? "";
            _vm.StatusText    = cached.Status;
        }
    }

    private void ApplyMissionToViewModel(MissionDetailDto mission)
    {
        _vm.Reference = mission.Reference;
        _vm.CallReference = string.IsNullOrEmpty(mission.CallReference) ? "" : $"Appel {mission.CallReference}";
        _vm.TargetAddress = mission.TargetAddress;
        _vm.LocationDetail = mission.LocationDetail ?? "";
        _vm.HasLocationDetail = !string.IsNullOrEmpty(mission.LocationDetail);
        _vm.BriefingText = mission.BriefingText ?? "";
        _vm.NarrativeReport = mission.NarrativeReport ?? "";
        _vm.HasNarrativeReport = !string.IsNullOrEmpty(mission.NarrativeReport);
        _vm.TargetLat = mission.TargetLatitude;
        _vm.TargetLng = mission.TargetLongitude;
        _vm.CreatedAtText = $"Créée le {mission.CreatedAt.ToLocalTime():dd/MM/yyyy à HH:mm}";

        if (mission.DispatchedAt.HasValue)
        {
            _vm.DispatchedAtText = $"Dispatchée à {mission.DispatchedAt.Value.ToLocalTime():HH:mm}";
            _vm.HasDispatchedAt = true;
        }
        if (mission.ArrivedAt.HasValue)
        {
            _vm.ArrivedAtText = $"Arrivée sur place à {mission.ArrivedAt.Value.ToLocalTime():HH:mm}";
            _vm.HasArrivedAt = true;
        }
        if (mission.CompletedAt.HasValue)
        {
            _vm.CompletedAtText = $"Clôturée à {mission.CompletedAt.Value.ToLocalTime():HH:mm}";
            _vm.HasCompletedAt = true;
        }

        _vm.CompletionReport = mission.CompletionReport ?? "";
        _vm.HasCompletionReport = !string.IsNullOrEmpty(mission.CompletionReport);

        _vm.Priority = mission.Priority;

        (_vm.StatusText, _vm.StatusColor) = mission.Status switch
        {
            "Pending"    => ("En attente",  Color.FromArgb("#f59e0b")),
            "Proposed"   => ("Proposée",    Color.FromArgb("#f59e0b")),
            "Accepted"   => ("Acceptée",    Color.FromArgb("#22c55e")),
            "InProgress" => ("En cours",    Color.FromArgb("#3b82f6")),
            "Completed"  => ("Terminée",    Color.FromArgb("#6b7280")),
            "Cancelled"  => ("Annulée",     Color.FromArgb("#ef4444")),
            _            => (mission.Status, Colors.White)
        };

        var pending = mission.Assignments?
            .FirstOrDefault(a => a.Status is "Proposed" or "Pending");
        _vm.AssignmentId = pending?.Id;
        _vm.ShowAcceptRefuse = pending != null;
        _vm.ShowComplete = mission.Status is "Accepted" or "InProgress";

        _vm.Intervenants = new ObservableCollection<IntervenantVm>(
            (mission.Intervenants ?? [])
                .OrderBy(i => i.Order)
                .Select(i => new IntervenantVm
                {
                    FullName    = i.FullName,
                    Role        = i.Role,
                    PhoneNumber = i.PhoneNumber,
                    IsInjured   = i.IsInjured,
                    Notes       = i.Notes
                }));

        _vm.Assignments = new ObservableCollection<AssignmentSummaryVm>(
            (mission.Assignments ?? [])
                .OrderBy(a => a.ProposalOrder)
                .Select(a =>
                {
                    var (color, label) = a.Status switch
                    {
                        "Accepted" => (Color.FromArgb("#22c55e"), "Accepté"),
                        "Refused"  => (Color.FromArgb("#ef4444"), "Refusé"),
                        _          => (Color.FromArgb("#f59e0b"), "Proposé")
                    };
                    var detail = $"#{a.ProposalOrder} — {a.VehicleCallSign} — {a.ProposedAt.ToLocalTime():HH:mm}";
                    if (!string.IsNullOrEmpty(a.RefusalReasonCode)) detail += $" — {RefusalCodeToLabel(a.RefusalReasonCode)}";
                    if (!string.IsNullOrEmpty(a.RefusalReason)) detail += $" : {a.RefusalReason}";
                    return new AssignmentSummaryVm
                    {
                        Summary     = $"Véhicule {a.VehicleCallSign} ({label})",
                        Detail      = detail,
                        StatusColor = color
                    };
                }));
    }

    private async Task ComputeDistanceAsync()
    {
        try
        {
            var loc = await Geolocation.Default.GetLastKnownLocationAsync()
                   ?? await Geolocation.Default.GetLocationAsync(
                          new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5)));
            if (loc == null) { _vm.DistanceText = "Position non disponible"; return; }
            var km = Haversine(loc.Latitude, loc.Longitude, _vm.TargetLat, _vm.TargetLng);
            _vm.DistanceText = $"Distance estimée : {km:F1} km";
        }
        catch { _vm.DistanceText = "Distance non disponible"; }
    }

    private async void OnOpenGps(object sender, EventArgs e)
    {
        try
        {
            var location = new Location(_vm.TargetLat, _vm.TargetLng);
            var options = new MapLaunchOptions { Name = _vm.TargetAddress };
            await Map.Default.OpenAsync(location, options);
        }
        catch { await DisplayAlert("Erreur", "Impossible d'ouvrir l'application GPS.", "OK"); }
    }

    private async void OnAccept(object sender, EventArgs e)
    {
        if (_vm.AssignmentId == null) return;
        try
        {
            await _api.PostAsync(
                $"api/missions/{_missionId}/assignments/{_vm.AssignmentId}/accept", null);
            _vm.ShowAcceptRefuse = false;
            _vm.ShowComplete = true;
            _vm.StatusText = "Acceptée";
            _vm.StatusColor = Color.FromArgb("#22c55e");
        }
        catch { await DisplayAlert("Erreur", "Impossible d'accepter la mission.", "OK"); }
    }

    private static readonly (string Label, string Code, bool NeedsText)[] RefusalOptions =
    [
        ("Véhicule en panne",          "VehicleBroken",    false),
        ("Trop loin",                  "TooFar",           false),
        ("Hors zone de patrouille",    "OutOfZone",        false),
        ("Sur une autre mission",      "OnAnotherMission", false),
        ("En pause",                   "OnBreak",          true),
        ("Non disponible",             "Unavailable",      true),
        ("Autre",                      "Other",            true),
    ];

    private async void OnRefuse(object sender, EventArgs e)
    {
        if (_vm.AssignmentId == null) return;

        var labels = RefusalOptions.Select(r => r.Label).ToArray();
        var selected = await DisplayActionSheet("Motif de refus", "Annuler", null, labels);
        if (selected == null || selected == "Annuler") return;

        var option = RefusalOptions.FirstOrDefault(r => r.Label == selected);
        if (option == default) return;

        string? freeText = null;
        if (option.NeedsText)
        {
            freeText = await DisplayPromptAsync(
                "Précision", "Description (optionnel) :", "Confirmer", "Annuler",
                maxLength: 200);
            if (freeText == null) return;
        }

        try
        {
            await _api.PostAsync(
                $"api/missions/{_missionId}/assignments/{_vm.AssignmentId}/refuse",
                new { reasonCode = option.Code, reason = string.IsNullOrWhiteSpace(freeText) ? option.Label : freeText });
            await Navigation.PopAsync();
        }
        catch { await DisplayAlert("Erreur", "Impossible de refuser la mission.", "OK"); }
    }

    private async void OnComplete(object sender, EventArgs e)
    {
        var notes = NotesEditor.Text ?? "";

        if (!_connectivity.IsConnected)
        {
            // Sauvegarder localement comme entrée de suivi en attente
            await _localDb.AddPendingEntryAsync(new PendingTrackingEntry
            {
                MissionId = _missionId,
                EntryType = "note",
                Content   = notes,
                CreatedAt = DateTime.UtcNow,
                IsSynced  = false
            });

            await DisplayAlert("Hors ligne",
                "Rapport sauvegardé localement. Il sera envoyé automatiquement dès le retour du réseau.", "OK");
            return;
        }

        try
        {
            await _api.PostAsync($"api/missions/{_missionId}/complete", new { report = notes });
            _vm.ShowComplete = false;
            _vm.StatusText = "Terminée";
            _vm.StatusColor = Color.FromArgb("#6b7280");
            await DisplayAlert("Mission terminée", "La mission a été clôturée avec succès.", "OK");
            await Navigation.PopAsync();
        }
        catch { await DisplayAlert("Erreur", "Impossible de terminer la mission.", "OK"); }
    }

    private static string RefusalCodeToLabel(string? code) => code switch
    {
        "VehicleBroken"    => "Véhicule en panne",
        "TooFar"           => "Trop loin",
        "OutOfZone"        => "Hors zone",
        "OnAnotherMission" => "Autre mission",
        "OnBreak"          => "En pause",
        "Unavailable"      => "Non disponible",
        "Other"            => "Autre",
        _                  => code ?? ""
    };

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private class MissionDetailDto
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string CallReference { get; set; } = "";
        public string Status { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public string? LocationDetail { get; set; }
        public string? BriefingText { get; set; }
        public string? NarrativeReport { get; set; }
        public double TargetLatitude { get; set; }
        public double TargetLongitude { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? ArrivedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletionReport { get; set; }
        public string Priority { get; set; } = "Routine";
        public List<AssignmentDetailDto>? Assignments { get; set; }
        public List<IntervenantDetailDto>? Intervenants { get; set; }
    }

    private class AssignmentDetailDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "";
        public int ProposalOrder { get; set; }
        public string VehicleCallSign { get; set; } = "";
        public DateTime ProposedAt { get; set; }
        public string? RefusalReasonCode { get; set; }
        public string? RefusalReason { get; set; }
    }

    private class IntervenantDetailDto
    {
        public string FullName { get; set; } = "";
        public string? Role { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsInjured { get; set; }
        public string? Notes { get; set; }
        public int Order { get; set; }
    }
}
