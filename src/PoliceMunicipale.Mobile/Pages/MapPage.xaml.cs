namespace PoliceMunicipale.Mobile.Pages;

public partial class MapPage : ContentPage
{
    public MapPage()
    {
        InitializeComponent();
        LoadMap();
    }

    private void LoadMap()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <head>
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
              <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
              <style>body{margin:0}#map{height:100vh}</style>
            </head>
            <body>
              <div id="map"></div>
              <script>
                var map = L.map('map').setView([43.6, 1.44], 14);
                L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);

                if (navigator.geolocation) {
                  navigator.geolocation.getCurrentPosition(function(pos) {
                    map.setView([pos.coords.latitude, pos.coords.longitude], 15);
                    L.marker([pos.coords.latitude, pos.coords.longitude])
                     .addTo(map).bindPopup('Ma position').openPopup();
                  });
                }
              </script>
            </body>
            </html>
            """;
        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }
}
