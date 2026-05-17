using System.Globalization;
using System.Text.Json;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Pages;

[QueryProperty(nameof(CenterLat), "centerLat")]
[QueryProperty(nameof(CenterLng), "centerLng")]
[QueryProperty(nameof(MarkerName), "markerName")]
public partial class MapPage : ContentPage
{
    private readonly ApiService _api;
    private bool _htmlLoaded;
    private bool _mapReady;
    private double? _pendingLat, _pendingLng;
    private string? _pendingName;
    private double _focusLat, _focusLng;

    public MapPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    // Called by Shell before OnAppearing when navigating with query params
    public string CenterLat
    {
        set
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                _pendingLat = v;
        }
    }

    public string CenterLng
    {
        set
        {
            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                _pendingLng = v;
        }
    }

    public string MarkerName { set => _pendingName = Uri.UnescapeDataString(value ?? ""); }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_htmlLoaded)
        {
            _htmlLoaded = true;
            LoadMap();
            // OnMapNavigated will apply pending focus after streets are loaded
        }
        else if (_pendingLat.HasValue && _pendingLng.HasValue && _mapReady)
        {
            _ = ApplyFocusAsync(_pendingLat.Value, _pendingLng.Value, _pendingName);
            ClearPending();
        }
    }

    private void LoadMap()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
              <style>html,body{margin:0;padding:0;height:100%}#map{height:100%}</style>
            </head>
            <body>
              <div id="map"></div>

              <script>
                /* --- Queue for ops that arrive before the map is initialized --- */
                var map = null;
                var focusMarker = null;
                var mapReady = false;
                var pendingOps = [];

                function runWhenReady(fn) {
                  if (mapReady) { fn(); }
                  else { pendingOps.push(fn); }
                }

                function addStreets(streets) {
                  if (!streets) return;
                  runWhenReady(function() {
                    streets.forEach(function(s) {
                      var score = s.currentRiskScore || 0;
                      var color = score > 70 ? '#ef4444' :
                                  score > 40 ? '#f59e0b' :
                                  score > 20 ? '#eab308' : '#22c55e';
                      L.polyline(
                        [[s.startLatitude, s.startLongitude], [s.endLatitude, s.endLongitude]],
                        { color: color, weight: 6, opacity: 0.85 }
                      ).bindPopup(
                        '<b>' + s.name + '</b>' +
                        (s.district ? '<br>' + s.district : '') +
                        '<br>Risque : <b>' + score + '</b>'
                      ).addTo(map);
                    });
                  });
                }

                function setCenter(lat, lng, zoom) {
                  runWhenReady(function() { map.setView([lat, lng], zoom || 14); });
                }

                function setFocusMarker(lat, lng, name) {
                  runWhenReady(function() {
                    if (focusMarker) { map.removeLayer(focusMarker); }
                    focusMarker = L.marker([lat, lng], {
                      icon: L.icon({
                        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png',
                        shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.9.4/images/marker-shadow.png',
                        iconSize: [25, 41], iconAnchor: [12, 41], popupAnchor: [1, -34]
                      })
                    });
                    if (name) focusMarker.bindPopup('<b>' + name + '</b>').openPopup();
                    focusMarker.addTo(map);
                    map.setView([lat, lng], 17);
                  });
                }
              </script>

              <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>

              <script>
                /* --- Initialize map only once Leaflet and all resources are loaded --- */
                window.addEventListener('load', function() {
                  try {
                    map = L.map('map').setView([43.6047, 1.4442], 14);
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                      attribution: '© OpenStreetMap contributors', maxZoom: 19
                    }).addTo(map);
                    mapReady = true;
                    pendingOps.forEach(function(fn) { fn(); });
                    pendingOps = [];
                  } catch(e) {
                    document.body.innerHTML = '<p style="color:red;padding:20px">Erreur carte: ' + e.message + '</p>';
                  }
                });
              </script>
            </body>
            </html>
            """;

        MapWebView.Source = new HtmlWebViewSource { Html = html };
        MapWebView.Navigated += OnMapNavigated;
    }

    private async void OnMapNavigated(object? sender, WebNavigatedEventArgs e)
    {
        MapWebView.Navigated -= OnMapNavigated;
        if (e.Result != WebNavigationResult.Success) return;
        _mapReady = true;
        await LoadStreetsOnMapAsync();

        if (_pendingLat.HasValue && _pendingLng.HasValue)
        {
            await ApplyFocusAsync(_pendingLat.Value, _pendingLng.Value, _pendingName);
            ClearPending();
        }
    }

    private async Task LoadStreetsOnMapAsync()
    {
        try
        {
            var streets = await _api.GetAsync<List<StreetMapDto>>("api/streets");
            if (streets == null || streets.Count == 0) return;

            var json = JsonSerializer.Serialize(streets, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await MapWebView.EvaluateJavaScriptAsync($"addStreets({json})");

            // Center on first street only if no pending focus
            if (!_pendingLat.HasValue)
            {
                var first = streets[0];
                var lat = (first.StartLatitude + first.EndLatitude) / 2;
                var lng = (first.StartLongitude + first.EndLongitude) / 2;
                await MapWebView.EvaluateJavaScriptAsync($"setCenter({lat}, {lng}, 14)");
            }
        }
        catch { /* map still works without streets */ }
    }

    private async Task ApplyFocusAsync(double lat, double lng, string? name)
    {
        _focusLat = lat;
        _focusLng = lng;
        var escapedName = (name ?? "").Replace("'", "\\'");
        var latStr = lat.ToString("F6", CultureInfo.InvariantCulture);
        var lngStr = lng.ToString("F6", CultureInfo.InvariantCulture);
        await MapWebView.EvaluateJavaScriptAsync($"setFocusMarker({latStr}, {lngStr}, '{escapedName}')");
        DirectionsButton.IsVisible = true;
    }

    private void ClearPending()
    {
        _pendingLat = null;
        _pendingLng = null;
        _pendingName = null;
    }

    private async void OnDirectionsClicked(object? sender, EventArgs e)
    {
        try
        {
            var location = new Location(_focusLat, _focusLng);
            var options = new MapLaunchOptions { Name = _pendingName ?? "Destination" };
            await Map.Default.OpenAsync(location, options);
        }
        catch { await DisplayAlert("Erreur", "Impossible d'ouvrir l'application GPS.", "OK"); }
    }

    private class StreetMapDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string? District { get; set; }
        public int CurrentRiskScore { get; set; }
        public double StartLatitude { get; set; }
        public double StartLongitude { get; set; }
        public double EndLatitude { get; set; }
        public double EndLongitude { get; set; }
    }
}
