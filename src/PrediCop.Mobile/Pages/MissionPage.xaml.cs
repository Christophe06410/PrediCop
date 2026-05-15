using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Pages;

public partial class MissionPage : ContentPage
{
    private readonly ApiService _api;
    private readonly AuthService _auth;
    private Guid? _currentAssignmentId;
    private Guid? _currentMissionId;

    public MissionPage(ApiService api, AuthService auth)
    {
        InitializeComponent();
        _api = api;
        _auth = auth;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadCurrentMission();
    }

    private async void LoadCurrentMission()
    {
        try
        {
            var missions = await _api.GetAsync<List<MissionInfo>>("api/missions/active");
            if (missions?.Any() == true)
                ShowActiveMission(missions.First());
            else
                ShowNoMission();
        }
        catch { ShowNoMission(); }
    }

    public void ShowMissionProposal(MissionInfo mission)
    {
        _currentAssignmentId = mission.AssignmentId;
        _currentMissionId = mission.MissionId;
        MissionAddressLabel.Text = mission.Address;
        MissionDescriptionLabel.Text = mission.Description;
        MissionDistanceLabel.Text = $"Distance estimée: {mission.DistanceKm:F1} km";

        MissionProposalFrame.IsVisible = true;
        ActiveMissionFrame.IsVisible = false;
        NoMissionFrame.IsVisible = false;
    }

    private void ShowActiveMission(MissionInfo mission)
    {
        _currentMissionId = mission.MissionId;
        ActiveMissionRef.Text = mission.Reference;
        ActiveMissionAddress.Text = mission.Address;
        ActiveMissionBriefing.Text = mission.Briefing;

        ActiveMissionFrame.IsVisible = true;
        MissionProposalFrame.IsVisible = false;
        NoMissionFrame.IsVisible = false;
    }

    private void ShowNoMission()
    {
        NoMissionFrame.IsVisible = true;
        MissionProposalFrame.IsVisible = false;
        ActiveMissionFrame.IsVisible = false;
    }

    private async void OnAvailabilityToggled(object sender, ToggledEventArgs e)
    {
        var status = e.Value ? "Available" : "Busy";
        StatusLabel.Text = e.Value ? "DISPONIBLE" : "OCCUPÉ";
        StatusFrame.BackgroundColor = e.Value ? Color.FromArgb("#22c55e") : Color.FromArgb("#f59e0b");

        // TODO: get vehicleId from session
        // await _api.PutAsync<object>("api/vehicles/{vehicleId}/status", new { status });
    }

    private async void OnAcceptMission(object sender, EventArgs e)
    {
        if (_currentMissionId == null || _currentAssignmentId == null) return;
        try
        {
            await _api.PostAsync($"api/missions/{_currentMissionId}/assignments/{_currentAssignmentId}/accept", null);
            LoadCurrentMission();
        }
        catch { await DisplayAlert("Erreur", "Impossible d'accepter la mission.", "OK"); }
    }

    private async void OnRefuseMission(object sender, EventArgs e)
    {
        if (_currentMissionId == null || _currentAssignmentId == null) return;
        var reason = await DisplayPromptAsync("Refus de mission", "Raison (optionnel):", "Refuser", "Annuler");
        if (reason == null) return;
        try
        {
            await _api.PostAsync($"api/missions/{_currentMissionId}/assignments/{_currentAssignmentId}/refuse",
                new { reason });
            ShowNoMission();
        }
        catch { await DisplayAlert("Erreur", "Impossible de refuser la mission.", "OK"); }
    }

    private async void OnCompleteMission(object sender, EventArgs e)
    {
        if (_currentMissionId == null) return;
        var report = await DisplayPromptAsync("Fin de mission", "Rapport de fin de mission:", "Terminer", "Annuler", "");
        if (report == null) return;
        try
        {
            await _api.PostAsync($"api/missions/{_currentMissionId}/complete", new { report });
            ShowNoMission();
        }
        catch { await DisplayAlert("Erreur", "Impossible de terminer la mission.", "OK"); }
    }
}

public record MissionInfo(
    Guid MissionId,
    Guid? AssignmentId,
    string Reference,
    string Address,
    string Description,
    string Briefing,
    double DistanceKm,
    double Latitude,
    double Longitude
);
