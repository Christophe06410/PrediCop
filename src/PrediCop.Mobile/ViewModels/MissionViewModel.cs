using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PrediCop.Mobile.Messages;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class MissionViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly MediaUploadService _mediaUpload;
    private readonly AuthService _auth;
    private readonly IAlertSoundService _alertSound;
    private readonly MissionAlertService _missionAlert;
    private readonly SignalRService _signalR;
    private Guid? _currentMissionId;
    private Guid? _currentAssignmentId;
    // Données sauvegardées de la proposition courante pour pouvoir basculer en actif sans re-fetch
    private string _proposalReference = "";
    private double _proposalLat, _proposalLng;
    // Missions non formellement attribuées que l'équipage a explicitement fermées
    private readonly HashSet<Guid> _dismissedSoftProposalIds = [];

    public MissionViewModel(ApiService api, MediaUploadService mediaUpload, AuthService auth,
        SignalRService signalR, IAlertSoundService alertSound, MissionAlertService missionAlert)
    {
        _api = api;
        _mediaUpload = mediaUpload;
        _auth = auth;
        _alertSound = alertSound;
        _missionAlert = missionAlert;
        _signalR = signalR;

        // Singleton : on s'abonne une seule fois, le VM survit aux changements d'onglet.
        signalR.MissionProposed += OnSignalRMissionProposed;
        signalR.MissionStatusChanged += OnSignalRMissionStatusChanged;
        signalR.Reconnected += (_, _) => MainThread.BeginInvokeOnMainThread(async () =>
            await LoadCurrentMissionAsync());

        // Filet de sécurité : quand le temps réel (SignalR) est indisponible, on poll l'API
        // pour ne pas rater une mission proposée pendant la coupure.
        _ = StartRealtimeFallbackAsync();
    }

    /// <summary>
    /// Polling de secours : tant que SignalR n'est pas connecté, rafraîchit la mission courante
    /// toutes les 15 s. Inactif quand le temps réel fonctionne (les push suffisent).
    /// </summary>
    private async Task StartRealtimeFallbackAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync())
        {
            if (_signalR.IsConnected) continue;              // temps réel OK → rien à faire
            if (string.IsNullOrEmpty(_auth.Token)) continue; // pas connecté → pas d'appel API
            await MainThread.InvokeOnMainThreadAsync(LoadCurrentMissionAsync);
        }
    }

    private void OnSignalRMissionProposed(object? sender, MissionProposedArgs e)
    {
        // Ack immédiat → annule le timer Firebase côté serveur
        if (e.AssignmentId != Guid.Empty)
            _ = _signalR.AckMissionNotificationAsync(e.AssignmentId);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
#if DEBUG
            MobileLogger.Log("MissionProposed", "SignalR event received");
#endif
            await LoadCurrentMissionAsync();

            if (AppPreferences.AlertSoundEnabled)
            {
                try { _alertSound.PlayAlert(); } catch { }
                try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(800)); } catch { }
            }

            // Bannière uniquement si l'utilisateur n'est pas déjà sur la page missions
            var location = Shell.Current.CurrentState.Location.ToString();
            var showBanner = !location.Contains("missions");
#if DEBUG
            MobileLogger.Log("MissionAlert", $"location={location}, showBanner={showBanner}, showProposal={ShowMissionProposal}");
#endif
            if (showBanner)
            {
                var (title, address) = ShowMissionProposal
                    ? ("NOUVELLE MISSION À ACCEPTER", ProposalAddress)
                    : ("MISSION ASSIGNÉE", ActiveMissionAddress);
#if DEBUG
                MobileLogger.Log("MissionAlert", $"Show called: {title} / {address}");
#endif
                _missionAlert.Show(title, address);
            }
        });
    }

    private void OnSignalRMissionStatusChanged(object? sender, string e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadCurrentMissionAsync();
            // Si plus aucune proposition en attente → la mission a été acceptée/refusée/annulée
            if (!ShowMissionProposal)
            {
                _alertSound.StopAlert();
                _missionAlert.Dismiss();
            }
        });
    }

    // Status bar
    [ObservableProperty] private bool isAvailable = true;
    [ObservableProperty] private string statusText = "DISPONIBLE";
    [ObservableProperty] private Color statusColor = Color.FromArgb("#22c55e");
    [ObservableProperty] private string vehicleLabel = "Véhicule: --";
    [ObservableProperty] private bool showAssignVehicleButton;

    // Frame visibility
    [ObservableProperty] private bool showMissionProposal;
    [ObservableProperty] private bool showActiveMission;
    [ObservableProperty] private bool showNoMission = true;

    // Mission proposal
    [ObservableProperty] private string proposalAddress = "";
    [ObservableProperty] private string proposalDescription = "";
    [ObservableProperty] private string proposalDistance = "";

    // Formal vs. soft proposal
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSoftProposal))]
    [NotifyPropertyChangedFor(nameof(ProposalTitle))]
    [NotifyPropertyChangedFor(nameof(ProposalFrameColor))]
    private bool isFormalProposal;
    public bool IsSoftProposal => !IsFormalProposal;
    public string ProposalTitle => IsFormalProposal ? "NOUVELLE MISSION" : "EN ATTENTE DE DISPATCH";
    public Color ProposalFrameColor => IsFormalProposal ? Color.FromArgb("#dc2626") : Color.FromArgb("#92400e");

    // Active mission
    [ObservableProperty] private string activeMissionRef = "";
    [ObservableProperty] private string activeMissionAddress = "";
    [ObservableProperty] private string activeMissionBriefing = "";

    // Priorité mission active
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveMissionPriorityColor))]
    [NotifyPropertyChangedFor(nameof(ActiveMissionPriorityEmoji))]
    [NotifyPropertyChangedFor(nameof(ActiveMissionHasPriorityBanner))]
    [NotifyPropertyChangedFor(nameof(ActiveMissionSosBannerColor))]
    [NotifyPropertyChangedFor(nameof(ActiveMissionSosBannerText))]
    private string activeMissionPriority = "Routine";

    public Color ActiveMissionPriorityColor => ActiveMissionPriority switch
    {
        "SOS"      => Colors.Red,
        "Critique" => Color.FromArgb("#dc2626"),
        "Urgent"   => Color.FromArgb("#f59e0b"),
        _          => Colors.Gray
    };
    public string ActiveMissionPriorityEmoji => ActiveMissionPriority switch
    {
        "SOS"      => "🚨",
        "Critique" => "⚠️",
        "Urgent"   => "❗",
        _          => ""
    };
    public bool HasActiveMissionPriorityLabel => ActiveMissionPriority is not "Routine" and not "";
    public bool ActiveMissionHasPriorityBanner => ActiveMissionPriority is "SOS" or "Critique" or "Urgent";
    public Color ActiveMissionSosBannerColor   => ActiveMissionPriority is "SOS" or "Critique" ? Colors.Red : Color.FromArgb("#d97706");
    public string ActiveMissionSosBannerText   => ActiveMissionPriority switch
    {
        "SOS"      => "🚨 MISSION SOS",
        "Critique" => "⚠️ MISSION CRITIQUE",
        "Urgent"   => "❗ MISSION URGENTE",
        _          => ""
    };

    // Priorité mission proposée
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProposalPriorityColor))]
    [NotifyPropertyChangedFor(nameof(ProposalPriorityEmoji))]
    private string proposalPriority = "Routine";
    public Color ProposalPriorityColor => ProposalPriority switch
    {
        "SOS"      => Colors.Red,
        "Critique" => Color.FromArgb("#dc2626"),
        "Urgent"   => Color.FromArgb("#f59e0b"),
        _          => Colors.Gray
    };
    public string ProposalPriorityEmoji => ProposalPriority switch
    {
        "SOS"      => "🚨",
        "Critique" => "⚠️",
        "Urgent"   => "❗",
        _          => ""
    };
    public bool HasProposalPriorityLabel => ProposalPriority is not "Routine" and not "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDistance))]
    private string activeMissionDistance = "";
    public bool HasActiveDistance => !string.IsNullOrEmpty(ActiveMissionDistance);

    private double _activeMissionLat, _activeMissionLng;
    public Guid? CurrentMissionId => _currentMissionId;

    // Upload state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotUploading))]
    private bool isUploading;

    [ObservableProperty] private double uploadProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUploadStatus))]
    private string uploadStatus = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhotoStatus))]
    private string photoStatus = "";

    /// <summary>Vrai si le véhicule est marqué OnMission sur le serveur sans mission visible — état fantôme.</summary>
    [ObservableProperty] private bool isVehicleStuck;

    public bool IsNotUploading => !IsUploading;
    public bool HasUploadStatus => !string.IsNullOrEmpty(UploadStatus);
    public bool HasPhotoStatus => !string.IsNullOrEmpty(PhotoStatus);

    partial void OnIsAvailableChanged(bool value)
    {
        StatusText = value ? "DISPONIBLE" : "OCCUPÉ";
        StatusColor = value ? Color.FromArgb("#22c55e") : Color.FromArgb("#f59e0b");
    }

    public void RefreshVehicleLabel()
    {
        VehicleLabel = _auth.VehicleCallSign is not null
            ? $"VL : {_auth.VehicleCallSign}"
            : "Aucun VL assigné";
        ShowAssignVehicleButton = _auth.VehicleCallSign is null
            && string.Equals(_auth.CurrentUser?.Role, "PatrolLeader", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    public async Task LoadCurrentMissionAsync()
    {
        RefreshVehicleLabel();
        try
        {
            var missions = await _api.GetAsync<List<ApiMissionDto>>("api/missions/active");
            if (missions?.Count > 0)
            {
                // Prioritize any mission that is actively proposed to this vehicle
                var proposed = missions
                    .Select(m => (Mission: m, Assignment: m.Assignments.FirstOrDefault(a => a.IsPending)))
                    .FirstOrDefault(x => x.Assignment != null);

                if (proposed.Mission != null && proposed.Assignment != null)
                {
                    // Dispatch formel : si l'équipage avait fermé cette mission en soft, l'enlever de la liste des ignorées
                    _dismissedSoftProposalIds.Remove(proposed.Mission.Id);
                    SetMissionProposal(new MissionInfo(
                        proposed.Mission.Id, proposed.Assignment.Id, proposed.Mission.Reference,
                        proposed.Mission.TargetAddress, proposed.Mission.BriefingText, proposed.Mission.BriefingText,
                        0, proposed.Mission.TargetLatitude, proposed.Mission.TargetLongitude,
                        proposed.Mission.Priority));
                }
                else
                {
                    var m = missions[0];
                    var myVehicleId = _auth.VehicleId;

                    var hasAcceptedAssignment = myVehicleId.HasValue
                        && m.Assignments.Any(a =>
                            a.VehicleId == myVehicleId.Value
                            && a.Status is "Accepted" or "InProgress");

                    if (hasAcceptedAssignment)
                    {
                        SetActiveMission(new MissionInfo(
                            m.Id, null, m.Reference,
                            m.TargetAddress, "", m.BriefingText,
                            0, m.TargetLatitude, m.TargetLongitude,
                            m.Priority));
                    }
                    else
                    {
                        // Pas de proposition formelle pour ce véhicule → rien à afficher.
                        // La mission sera visible uniquement quand le BO dispatche formellement
                        // et que le SignalR envoie MissionProposed avec un AssignmentId.
                        SetNoMission();
                    }
                }
            }
            else
                SetNoMission();
        }
        catch { SetNoMission(); }

        // Si aucune mission visible, vérifie si le véhicule est bloqué OnMission côté serveur
        if (ShowNoMission)
            _ = CheckVehicleStuckAsync();
        else
            IsVehicleStuck = false;
    }

    private async Task CheckVehicleStuckAsync()
    {
        if (!_auth.VehicleId.HasValue) { IsVehicleStuck = false; return; }
        try
        {
            var v = await _api.GetAsync<VehicleStatusDto>($"api/vehicles/{_auth.VehicleId}");
            IsVehicleStuck = v?.Status == "OnMission";
        }
        catch { IsVehicleStuck = false; }
    }

    [RelayCommand]
    private async Task ReleaseStickyVehicleAsync()
    {
        if (!_auth.VehicleId.HasValue) return;
        try
        {
            await _api.PostAsync($"api/vehicles/{_auth.VehicleId}/release", null);
            IsVehicleStuck = false;
            await LoadCurrentMissionAsync();
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Erreur", "Impossible de libérer le véhicule."));
        }
    }

    private class VehicleStatusDto { public string Status { get; set; } = ""; }

    // Private DTOs matching API JSON
    private class ApiMissionDto
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public string BriefingText { get; set; } = "";
        public double TargetLatitude { get; set; }
        public double TargetLongitude { get; set; }
        public string Priority { get; set; } = "Routine";
        public List<ApiAssignmentDto> Assignments { get; set; } = [];
    }

    private class ApiAssignmentDto
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public string Status { get; set; } = "";
        public bool IsPending => Status is "Proposed";
    }

    public void SetMissionProposal(MissionInfo mission)
    {
        _currentAssignmentId = mission.AssignmentId;
        _currentMissionId = mission.MissionId;
        _proposalReference = mission.Reference;
        _proposalLat = mission.Latitude;
        _proposalLng = mission.Longitude;
        ProposalAddress = mission.Address;
        ProposalDescription = mission.Description;
        ProposalDistance = $"Distance estimée: {mission.DistanceKm:F1} km";
        ProposalPriority = mission.Priority;
        IsFormalProposal = mission.AssignmentId.HasValue;
        ShowMissionProposal = true;
        ShowActiveMission = false;
        ShowNoMission = false;
    }

    private void SetActiveMission(MissionInfo mission)
    {
        _currentMissionId = mission.MissionId;
        _activeMissionLat = mission.Latitude;
        _activeMissionLng = mission.Longitude;
        ActiveMissionRef = mission.Reference;
        ActiveMissionAddress = mission.Address;
        ActiveMissionBriefing = mission.Briefing;
        ActiveMissionDistance = "";
        ActiveMissionPriority = mission.Priority;
        ShowActiveMission = true;
        ShowMissionProposal = false;
        ShowNoMission = false;
        _ = ComputeActiveDistanceAsync(mission.Latitude, mission.Longitude);
    }

    private async Task ComputeActiveDistanceAsync(double lat, double lng)
    {
        if (lat == 0 && lng == 0) return;
        try
        {
            var loc = await Geolocation.Default.GetLastKnownLocationAsync();
            if (loc is null || (loc.Latitude == 0 && loc.Longitude == 0)) return;
            var km = Haversine(loc.Latitude, loc.Longitude, lat, lng);
            if (km < 0.05)
                ActiveMissionDistance = "Distance : < 50 m";
            else
                ActiveMissionDistance = $"Distance : {km:F1} km";
        }
        catch { }
    }

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

    private void SetNoMission()
    {
        _alertSound.StopAlert();
        ShowNoMission = true;
        ShowMissionProposal = false;
        ShowActiveMission = false;
        IsFormalProposal = false;
        _currentMissionId = null;
        _currentAssignmentId = null;
    }

    [RelayCommand]
    private void DismissSoftProposal()
    {
        if (_currentMissionId.HasValue)
            _dismissedSoftProposalIds.Add(_currentMissionId.Value);
        SetNoMission();
    }

    [RelayCommand]
    private async Task AcceptMissionAsync()
    {
        if (_currentMissionId == null || _currentAssignmentId == null) { SetNoMission(); return; }

        // Stopper la sonnerie immédiatement — avant même la réponse serveur
        _alertSound.StopAlert();

        // Captures avant l'await (les champs peuvent changer si un autre event arrive)
        var missionId  = _currentMissionId.Value;
        var reference  = _proposalReference;
        var address    = ProposalAddress;
        var briefing   = ProposalDescription;
        var priority   = ProposalPriority;
        var lat        = _proposalLat;
        var lng        = _proposalLng;

        try
        {
            await _api.PostAsync(
                $"api/missions/{_currentMissionId}/assignments/{_currentAssignmentId}/accept", null);

            // Basculer en "mission active" directement sur le main thread avec les données
            // déjà connues — évite un LoadCurrentMissionAsync depuis un thread pool qui peut
            // interférer avec le MissionStatusChanged SignalR arrivant au même moment.
            await MainThread.InvokeOnMainThreadAsync(() =>
                SetActiveMission(new MissionInfo(missionId, null, reference,
                    address, briefing, briefing, 0, lat, lng, priority)));
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Erreur", "Impossible d'accepter la mission."));
        }
    }

    public async Task RefuseMissionAsync(string reasonCode, string reason)
    {
        if (_currentMissionId == null || _currentAssignmentId == null) { SetNoMission(); return; }
        _alertSound.StopAlert();
        try
        {
            await _api.PostAsync(
                $"api/missions/{_currentMissionId}/assignments/{_currentAssignmentId}/refuse",
                new { reasonCode, reason });
            SetNoMission();
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Erreur", "Impossible de refuser la mission."));
        }
    }

    public async Task CompleteMissionAsync(string report)
    {
        if (_currentMissionId == null) return;
        try
        {
            await _api.PostAsync($"api/missions/{_currentMissionId}/complete", new { report });
            SetNoMission();
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Erreur", "Impossible de terminer la mission."));
        }
    }

    [RelayCommand]
    private async Task UploadVideoAsync()
    {
        if (_currentMissionId == null || IsUploading) return;
        IsUploading = true;
        UploadStatus = "Envoi en cours...";
        try
        {
            var progress = new Progress<double>(p =>
            {
                UploadProgress = p;
                UploadStatus = $"Envoi: {p:P0}";
            });
            var ok = await _mediaUpload.PickAndUploadAsync(_currentMissionId.Value, progress: progress);
            UploadStatus = ok ? "Vidéo envoyée ✓" : "";
        }
        catch (InvalidOperationException ex) { UploadStatus = ex.Message; }
        catch { UploadStatus = "Erreur lors de l'envoi."; }
        finally { IsUploading = false; UploadProgress = 0; }
    }

    [RelayCommand]
    private async Task CapturePhotoAsync()
    {
        if (_currentMissionId == null) return;
        PhotoStatus = "Prise de photo...";
        try
        {
            var ok = await _mediaUpload.CaptureAndUploadPhotoAsync(_currentMissionId.Value);
            PhotoStatus = ok ? "Photo envoyée ✓" : "";
        }
        catch (InvalidOperationException ex) { PhotoStatus = ex.Message; }
        catch { PhotoStatus = "Erreur lors de l'envoi."; }
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        if (_currentMissionId == null) return;
        PhotoStatus = "Envoi en cours...";
        try
        {
            var progress = new Progress<double>(p => PhotoStatus = $"Envoi: {p:P0}");
            var ok = await _mediaUpload.PickAndUploadPhotoAsync(
                _currentMissionId.Value, progress: progress);
            PhotoStatus = ok ? "Photo envoyée ✓" : "";
        }
        catch (InvalidOperationException ex) { PhotoStatus = ex.Message; }
        catch { PhotoStatus = "Erreur lors de l'envoi."; }
    }
}
