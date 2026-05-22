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

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_htmlLoaded)
        {
            _htmlLoaded = true;
            await LoadMapAsync();
        }
        else if (_pendingLat.HasValue && _pendingLng.HasValue && _mapReady)
        {
            _ = ApplyFocusAsync(_pendingLat.Value, _pendingLng.Value, _pendingName);
            ClearPending();
        }
    }

    private static async Task<string> LoadAssetAsync(string fileName)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private async Task LoadMapAsync()
    {
        string leafletJs, leafletCss;
        try
        {
            leafletJs  = await LoadAssetAsync("leaflet.min.js");
            leafletCss = await LoadAssetAsync("leaflet.min.css");
        }
        catch
        {
            // Assets missing — fall back to showing an error
            leafletJs = leafletCss = "";
        }

        string html;
        if (!string.IsNullOrEmpty(leafletJs))
        {
            // Build HTML with StringBuilder to avoid $""" brace-escaping conflicts with JS
            var sb = new System.Text.StringBuilder();
            sb.Append("<!DOCTYPE html><html><head>");
            sb.Append("<meta charset='utf-8'/>");
            sb.Append("<meta name='viewport' content='width=device-width,initial-scale=1.0,maximum-scale=1.0'>");
            sb.Append("<style>");
            sb.Append(leafletCss);
            sb.Append(" html,body{margin:0;padding:0;height:100%;background:#1a2035}");
            sb.Append(" #map{height:100%}");
            sb.Append(" .leaflet-container{background:#1e293b}");
            sb.Append(" .leaflet-tile-pane{opacity:.85}");
            sb.Append(" #loading{position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);color:#60a5fa;font-family:sans-serif;text-align:center}");
            sb.Append(" .street-tip{background:#1e293b;color:#e2e8f0;border:1px solid #334155;font-size:13px;font-weight:600;padding:3px 8px;box-shadow:0 2px 6px rgba(0,0,0,.5)}");
            sb.Append(" .street-tip::before{display:none}");
            sb.Append("</style></head><body>");
            sb.Append("<div id='loading'>Chargement de la carte...</div>");
            sb.Append("<div id='map'></div>");
            sb.Append("<script>"); sb.Append(leafletJs); sb.Append("</script>");
            sb.Append("<script>"); sb.Append(MapScript); sb.Append("</script>");
            sb.Append("</body></html>");
            html = sb.ToString();
        }
        else
        {
            html = "<html><body style='background:#1a2035;display:flex;align-items:center;justify-content:center;height:100vh;font-family:sans-serif;color:#60a5fa;text-align:center'>"
                 + "<div>Carte indisponible<br><small>Fichiers cartographiques manquants</small></div>"
                 + "</body></html>";
        }

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

    private const string MapScript = """
var map = null;
var focusMarker = null;
var mapReady = false;
var pendingOps = [];

function runWhenReady(fn) {
  if (mapReady) { fn(); } else { pendingOps.push(fn); }
}

function addStreets(streets) {
  if (!streets) return;
  runWhenReady(function() {
    streets.forEach(function(s) {
      var score = s.currentRiskScore || 0;
      var color = score > 70 ? '#ef4444' :
                  score > 40 ? '#f59e0b' :
                  score > 20 ? '#eab308' : '#22c55e';
      var line = L.polyline(
        [[s.startLatitude, s.startLongitude], [s.endLatitude, s.endLongitude]],
        { color: color, weight: 7, opacity: 0.9 }
      );
      line.bindTooltip(s.name, { sticky: true, direction: 'top', className: 'street-tip' });
      line.bindPopup('<b>' + s.name + '</b>' +
        (s.district ? '<br><span style="color:#93c5fd">' + s.district + '</span>' : '') +
        '<br>Risque : <b style="color:' + color + '">' + score + '</b>'
      );
      line.addTo(map);
    });
  });
}

function setCenter(lat, lng, zoom) {
  runWhenReady(function() { map.setView([lat, lng], zoom || 14); });
}

function setFocusMarker(lat, lng, name) {
  runWhenReady(function() {
    if (focusMarker) { map.removeLayer(focusMarker); }
    focusMarker = L.circleMarker([lat, lng], {
      radius: 14, color: '#ef4444', weight: 3,
      fillColor: '#ef4444', fillOpacity: 0.7
    });
    if (name) focusMarker.bindPopup('<b>' + name + '</b>').openPopup();
    focusMarker.addTo(map);
    map.setView([lat, lng], 17);
  });
}

try {
  document.getElementById('loading').style.display = 'none';
  map = L.map('map', { zoomControl: true }).setView([43.6047, 1.4442], 14);

  var tiles = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '&copy; OpenStreetMap contributors',
    maxZoom: 19, crossOrigin: true
  });
  var tileErrors = 0;
  tiles.on('tileerror', function() {
    tileErrors++;
    if (tileErrors === 3) {
      var info = document.createElement('div');
      info.style.cssText = 'position:absolute;bottom:8px;left:50%;transform:translateX(-50%);background:rgba(0,0,0,.6);color:#93c5fd;font-size:11px;padding:3px 8px;border-radius:4px;z-index:999;pointer-events:none';
      info.textContent = 'Fond de carte indisponible — données de risque actives';
      document.getElementById('map').appendChild(info);
    }
  });
  tiles.addTo(map);

  mapReady = true;
  pendingOps.forEach(function(fn) { fn(); });
  pendingOps = [];
} catch(e) {
  document.getElementById('loading').style.display = 'block';
  document.getElementById('loading').textContent = 'Erreur carte: ' + e.message;
}
""";

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
