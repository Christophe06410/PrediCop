using System.Text.Json;
using System.Text.Json.Serialization;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Pages;

public partial class MissionHistoryPage : ContentPage
{
    private readonly ApiService _api;
    private readonly LocalDbService _localDb;
    private readonly IConnectivityService _connectivity;
    private readonly SyncService _syncService;

    private DateOnly _currentDay = DateOnly.FromDateTime(DateTime.Today);
    private bool _showingRange = true; // vue initiale = 3 derniers jours
    private List<MissionItem> _items = [];

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public MissionHistoryPage(ApiService api, LocalDbService localDb,
        IConnectivityService connectivity, SyncService syncService)
    {
        InitializeComponent();
        _api = api;
        _localDb = localDb;
        _connectivity = connectivity;
        _syncService = syncService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var fr = new System.Globalization.CultureInfo("fr-FR");
        if (_showingRange)
        {
            DateLabel.Text = "3 derniers jours";
        }
        else
        {
            DateLabel.Text = _currentDay == DateOnly.FromDateTime(DateTime.Today)
                ? "Aujourd'hui"
                : _currentDay.ToString("dddd d MMMM", fr);
        }

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        MissionsCollection.ItemsSource = null;

        try
        {
            DateTime utcFrom, utcTo;
            if (_showingRange)
            {
                // 3 derniers jours (aujourd'hui inclus)
                var rangeStart = DateTime.Today.AddDays(-2);
                utcFrom = new DateTime(rangeStart.Year, rangeStart.Month, rangeStart.Day,
                    0, 0, 0, DateTimeKind.Local).ToUniversalTime();
                utcTo = DateTime.Today.AddDays(1).ToUniversalTime();
            }
            else
            {
                var localStart = new DateTime(_currentDay.Year, _currentDay.Month, _currentDay.Day,
                    0, 0, 0, DateTimeKind.Local);
                utcFrom = localStart.ToUniversalTime();
                utcTo = localStart.AddDays(1).ToUniversalTime();
            }
            var url = $"api/missions?status=Completed&dateFrom={utcFrom:yyyy-MM-ddTHH:mm:ss}Z&dateTo={utcTo:yyyy-MM-ddTHH:mm:ss}Z&size=200";
            var result = await _api.GetAsync<PagedResult<ApiMissionDto>>(url);

            _items = (result?.Items ?? [])
                .OrderByDescending(m => m.CompletedAt ?? m.CreatedAt)
                .Select(m => new MissionItem(m))
                .ToList();

            MissionsCollection.ItemsSource = _items;
        }
        catch
        {
            await DisplayAlertAsync("Erreur", "Impossible de charger l'historique.", "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            RefreshView.IsRefreshing = false;
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e) => await LoadAsync();

    private async void OnPrevDay(object? sender, EventArgs e)
    {
        if (_showingRange)
        {
            // Sortir du mode 3 jours : reculer d'un jour avant la plage
            _showingRange = false;
            _currentDay = DateOnly.FromDateTime(DateTime.Today.AddDays(-3));
        }
        else
        {
            _currentDay = _currentDay.AddDays(-1);
        }
        await LoadAsync();
    }

    private async void OnNextDay(object? sender, EventArgs e)
    {
        if (_showingRange) return; // déjà sur la plage la plus récente
        _currentDay = _currentDay.AddDays(1);
        if (_currentDay >= DateOnly.FromDateTime(DateTime.Today))
        {
            // Retour à la vue 3 jours
            _showingRange = true;
            _currentDay = DateOnly.FromDateTime(DateTime.Today);
        }
        await LoadAsync();
    }

    private async void OnMissionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not MissionItem item) return;
        MissionsCollection.SelectedItem = null;
        await Navigation.PushAsync(new MissionDetailPage(item.Id, _api, _localDb, _connectivity, _syncService));
    }

    // ---- DTOs ----

    private class PagedResult<T>
    {
        public List<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
    }

    private class ApiMissionDto
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public string Priority { get; set; } = "Routine";
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // ---- View item ----

    private class MissionItem(ApiMissionDto m)
    {
        public Guid Id { get; } = m.Id;
        public string Reference { get; } = m.Reference;
        public string Address { get; } = m.TargetAddress;

        public string PriorityLabel { get; } = m.Priority switch
        {
            "SOS"      => "SOS",
            "Critique" => "CRITIQUE",
            "Urgent"   => "URGENT",
            _          => ""
        };

        public Color PriorityColor { get; } = m.Priority switch
        {
            "SOS" or "Critique" => Colors.Red,
            "Urgent"            => Color.FromArgb("#f59e0b"),
            _                   => Colors.Transparent
        };

        public string TimeLabel { get; } = m.CompletedAt.HasValue
            ? $"Terminée le {m.CompletedAt.Value.ToLocalTime():dd/MM à HH:mm}"
            : $"Créée le {m.CreatedAt.ToLocalTime():dd/MM à HH:mm}";
    }
}
