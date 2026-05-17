using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PrediCop.Mobile.Messages;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class MissionViewModel(ApiService api, MediaUploadService mediaUpload) : ObservableObject
{
    private Guid? _currentMissionId;
    private Guid? _currentAssignmentId;

    // Status bar
    [ObservableProperty] private bool isAvailable = true;
    [ObservableProperty] private string statusText = "DISPONIBLE";
    [ObservableProperty] private Color statusColor = Color.FromArgb("#22c55e");

    // Frame visibility
    [ObservableProperty] private bool showMissionProposal;
    [ObservableProperty] private bool showActiveMission;
    [ObservableProperty] private bool showNoMission = true;

    // Mission proposal
    [ObservableProperty] private string proposalAddress = "";
    [ObservableProperty] private string proposalDescription = "";
    [ObservableProperty] private string proposalDistance = "";

    // Active mission
    [ObservableProperty] private string activeMissionRef = "";
    [ObservableProperty] private string activeMissionAddress = "";
    [ObservableProperty] private string activeMissionBriefing = "";

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

    public bool IsNotUploading => !IsUploading;
    public bool HasUploadStatus => !string.IsNullOrEmpty(UploadStatus);
    public bool HasPhotoStatus => !string.IsNullOrEmpty(PhotoStatus);

    partial void OnIsAvailableChanged(bool value)
    {
        StatusText = value ? "DISPONIBLE" : "OCCUPÉ";
        StatusColor = value ? Color.FromArgb("#22c55e") : Color.FromArgb("#f59e0b");
    }

    [RelayCommand]
    public async Task LoadCurrentMissionAsync()
    {
        try
        {
            var missions = await api.GetAsync<List<ApiMissionDto>>("api/missions/active");
            if (missions?.Count > 0)
            {
                // Prioritize any mission that is actively proposed to this vehicle
                var proposed = missions
                    .Select(m => (Mission: m, Assignment: m.Assignments.FirstOrDefault(a => a.IsPending)))
                    .FirstOrDefault(x => x.Assignment != null);

                if (proposed.Mission != null && proposed.Assignment != null)
                {
                    SetMissionProposal(new MissionInfo(
                        proposed.Mission.Id, proposed.Assignment.Id, proposed.Mission.Reference,
                        proposed.Mission.TargetAddress, proposed.Mission.BriefingText, proposed.Mission.BriefingText,
                        0, proposed.Mission.TargetLatitude, proposed.Mission.TargetLongitude));
                }
                else
                {
                    var m = missions[0];
                    SetActiveMission(new MissionInfo(
                        m.Id, null, m.Reference,
                        m.TargetAddress, "", m.BriefingText,
                        0, m.TargetLatitude, m.TargetLongitude));
                }
            }
            else
                SetNoMission();
        }
        catch { SetNoMission(); }
    }

    // Private DTOs matching API JSON
    private class ApiMissionDto
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public string BriefingText { get; set; } = "";
        public double TargetLatitude { get; set; }
        public double TargetLongitude { get; set; }
        public List<ApiAssignmentDto> Assignments { get; set; } = [];
    }

    private class ApiAssignmentDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = "";
        // Assignment is pending a response from this vehicle
        public bool IsPending => Status is "Proposed";
    }

    public void SetMissionProposal(MissionInfo mission)
    {
        _currentAssignmentId = mission.AssignmentId;
        _currentMissionId = mission.MissionId;
        ProposalAddress = mission.Address;
        ProposalDescription = mission.Description;
        ProposalDistance = $"Distance estimée: {mission.DistanceKm:F1} km";
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
        ShowActiveMission = true;
        ShowMissionProposal = false;
        ShowNoMission = false;
        _ = ComputeActiveDistanceAsync(mission.Latitude, mission.Longitude);
    }

    private async Task ComputeActiveDistanceAsync(double lat, double lng)
    {
        try
        {
            var loc = await Geolocation.Default.GetLastKnownLocationAsync();
            if (loc == null) return;
            ActiveMissionDistance = $"Distance : {Haversine(loc.Latitude, loc.Longitude, lat, lng):F1} km";
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
        ShowNoMission = true;
        ShowMissionProposal = false;
        ShowActiveMission = false;
    }

    [RelayCommand]
    private async Task AcceptMissionAsync()
    {
        if (_currentMissionId == null || _currentAssignmentId == null) return;
        try
        {
            await api.PostAsync(
                $"api/missions/{_currentMissionId}/assignments/{_currentAssignmentId}/accept", null);
            await LoadCurrentMissionAsync();
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Erreur", "Impossible d'accepter la mission."));
        }
    }

    public async Task RefuseMissionAsync(string reasonCode, string reason)
    {
        if (_currentMissionId == null || _currentAssignmentId == null) return;
        try
        {
            await api.PostAsync(
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
            await api.PostAsync($"api/missions/{_currentMissionId}/complete", new { report });
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
            var ok = await mediaUpload.PickAndUploadAsync(_currentMissionId.Value, progress: progress);
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
            var ok = await mediaUpload.CaptureAndUploadPhotoAsync(_currentMissionId.Value);
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
            var ok = await mediaUpload.PickAndUploadPhotoAsync(
                _currentMissionId.Value, progress: progress);
            PhotoStatus = ok ? "Photo envoyée ✓" : "";
        }
        catch (InvalidOperationException ex) { PhotoStatus = ex.Message; }
        catch { PhotoStatus = "Erreur lors de l'envoi."; }
    }
}
