using PoliceMunicipale.Mobile.Services;

namespace PoliceMunicipale.Mobile.Pages;

public partial class PatrolPage : ContentPage
{
    private readonly ApiService _api;

    public PatrolPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadPriorityStreets();
    }

    private async void LoadPriorityStreets()
    {
        try
        {
            var streets = await _api.GetAsync<List<StreetViewModel>>("api/streets/priority?count=20");
            StreetsCollection.ItemsSource = streets ?? [];
        }
        catch { /* show empty state */ }
    }

    private async void OnRefreshClicked(object sender, EventArgs e) => LoadPriorityStreets();

    private async void OnPatrolledClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string idStr && Guid.TryParse(idStr, out var streetId))
        {
            try
            {
                await _api.PostAsync($"api/streets/{streetId}/patrol", null);
                LoadPriorityStreets();
            }
            catch { await DisplayAlert("Erreur", "Impossible d'enregistrer le passage.", "OK"); }
        }
    }
}

public class StreetViewModel
{
    public Guid StreetId { get; set; }
    public string Name { get; set; } = "";
    public string? District { get; set; }
    public int RiskScore { get; set; }
    public DateTime? LastPatrolledAt { get; set; }

    public string LastPatrolText => LastPatrolledAt.HasValue
        ? $"Dernière patrouille: {LastPatrolledAt.Value:dd/MM HH:mm}"
        : "Jamais patrouillée";

    public Color RiskColor => RiskScore switch
    {
        > 70 => Color.FromArgb("#ef4444"),
        > 40 => Color.FromArgb("#f59e0b"),
        > 20 => Color.FromArgb("#eab308"),
        _ => Color.FromArgb("#22c55e")
    };
}
