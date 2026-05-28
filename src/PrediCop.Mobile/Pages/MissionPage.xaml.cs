using CommunityToolkit.Mvvm.Messaging;
using PrediCop.Mobile.Messages;
using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class MissionPage : ContentPage
{
    public MissionViewModel ViewModel { get; }
    private readonly ApiService _api;
    private readonly LocalDbService _localDb;
    private readonly IConnectivityService _connectivity;
    private readonly SyncService _syncService;

    public MissionPage(MissionViewModel vm, ApiService api,
        LocalDbService localDb, IConnectivityService connectivity, SyncService syncService)
    {
        InitializeComponent();
        ViewModel = vm;
        BindingContext = vm;
        _api = api;
        _localDb = localDb;
        _connectivity = connectivity;
        _syncService = syncService;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Le VM est singleton et écoute déjà SignalR en continu — on rafraîchit juste
        // l'état visible quand la page (re)devient visible.
        ViewModel.LoadCurrentMissionCommand.Execute(null);

        WeakReferenceMessenger.Default.Unregister<AlertMessage>(this);
        WeakReferenceMessenger.Default.Register<AlertMessage>(this, async (_, m) =>
            await MainThread.InvokeOnMainThreadAsync(() => DisplayAlert(m.Title, m.Text, "OK")));
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    public void ShowMissionProposal(MissionInfo mission) =>
        ViewModel.SetMissionProposal(mission);

    private async void OnViewMissionDetails(object sender, EventArgs e)
    {
        if (ViewModel.CurrentMissionId is not { } missionId) return;
        await Navigation.PushAsync(new MissionDetailPage(missionId, _api, _localDb, _connectivity, _syncService));
    }

    private async void OnViewHistoryClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new MissionHistoryPage(_api, _localDb, _connectivity, _syncService));
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

    private async void OnRefuseMission(object sender, EventArgs e)
    {
        var labels = RefusalOptions.Select(r => r.Label).ToArray();
        var selected = await DisplayActionSheet("Motif de refus", "Annuler", null, labels);
        if (selected == null || selected == "Annuler") return;

        var option = RefusalOptions.FirstOrDefault(r => r.Label == selected);
        if (option == default) return;

        string freeText = option.Label;
        if (option.NeedsText)
        {
            var prompt = await DisplayPromptAsync(
                "Précision", "Description (optionnel) :", "Confirmer", "Annuler", maxLength: 200);
            if (prompt == null) return;
            if (!string.IsNullOrWhiteSpace(prompt)) freeText = prompt;
        }

        await ViewModel.RefuseMissionAsync(option.Code, freeText);
    }

    private async void OnCompleteMission(object sender, EventArgs e)
    {
        var report = await DisplayPromptAsync(
            "Fin de mission", "Rapport de fin de mission:", "Terminer", "Annuler", "");
        if (report == null) return;
        await ViewModel.CompleteMissionAsync(report);
    }
}
