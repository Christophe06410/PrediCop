using Microsoft.Extensions.Logging;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using PrediCop.Mobile.Pages;
using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
  #if WINDOWS
        var apiBaseUrl = "https://localhost:7229";
  #else
        var apiBaseUrl = "https://192.168.0.92:7229";
  #endif
        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
#else
        var apiBaseUrl = "https://predicop-gvb7fjbhhwe2h8bj.westeurope-01.azurewebsites.net";
        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
#endif

        builder.Services.AddSingleton(sp =>
        {
            var http = new HttpClient(httpHandler) { BaseAddress = new Uri(apiBaseUrl), Timeout = TimeSpan.FromSeconds(20) };
            var log = sp.GetRequiredService<ILogger<ApiService>>();
            return new ApiService(http, log);
        });

        builder.Services.AddSingleton(sp =>
        {
            var http = new HttpClient(httpHandler) { BaseAddress = new Uri(apiBaseUrl), Timeout = TimeSpan.FromMinutes(10) };
            return new MediaUploadService(http);
        });

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<MobileErrorService>();
        builder.Services.AddSingleton<TenantFeaturesService>();
        builder.Services.AddSingleton<GpsTrackingService>();
        builder.Services.AddSingleton(new SignalRService(apiBaseUrl));
        builder.Services.AddSingleton<MissionAlertService>();
        builder.Services.AddSingleton<ConnectionStatusService>();

        // Son de notification système (mission proposée)
#if ANDROID
        builder.Services.AddSingleton<IAlertSoundService, Platforms.Android.AlertSoundService>();
#elif IOS
        builder.Services.AddSingleton<IAlertSoundService, Platforms.iOS.AlertSoundService>();
#else
        builder.Services.AddSingleton<IAlertSoundService, NoopAlertSoundService>();
#endif

        // Offline mode
        builder.Services.AddSingleton<LocalDbService>();
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<SyncService>();

        // Push notifications Firebase (Android + iOS)
        builder.Services.AddSingleton<PushNotificationService>();

        // Plugin.BLE — BLE beacon vehicle auto-detection
        builder.Services.AddSingleton<IBluetoothLE>(CrossBluetoothLE.Current);
        builder.Services.AddSingleton<IAdapter>(CrossBluetoothLE.Current.Adapter);
        builder.Services.AddSingleton<BleVehicleScanner>();

        // ViewModels
        builder.Services.AddSingleton<LoginViewModel>();
        // MissionViewModel doit rester vivant en permanence pour écouter SignalR
        // même quand l'utilisateur est sur un autre onglet.
        builder.Services.AddSingleton<MissionViewModel>();
        builder.Services.AddTransient<PatrolViewModel>();
        builder.Services.AddTransient<PatrolActivationViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<BeaconPairingViewModel>();
        builder.Services.AddTransient<TicketingViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MissionPage>();
        builder.Services.AddTransient<PatrolPage>();
        builder.Services.AddTransient<PatrolActivationPage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<BeaconPairingPage>();
        builder.Services.AddTransient<TicketingPage>();
        // MissionDetailPage is instantiated manually (missionId is a runtime parameter)

#if DEBUG
        builder.Logging.AddDebug();
#endif

#if ANDROID
        // Ensure WebView can load external resources (CDN scripts for maps)
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping(
            "PrediCopWebViewSettings", (handler, _) =>
            {
                handler.PlatformView.Settings.JavaScriptEnabled = true;
                handler.PlatformView.Settings.DomStorageEnabled = true;
                handler.PlatformView.Settings.SetGeolocationEnabled(true);
                handler.PlatformView.Settings.MixedContentMode =
                    Android.Webkit.MixedContentHandling.AlwaysAllow;
            });
#endif

        var app = builder.Build();

        // Initialise le logger debug mobile (envoie les logs à la console de l'API)
#if DEBUG
        MobileLogger.Init(app.Services.GetRequiredService<ApiService>());
#endif

        // Câble MobileErrorService dans ApiService après résolution du container
        // (évite la dépendance circulaire à l'enregistrement)
        var apiService = app.Services.GetRequiredService<ApiService>();
        apiService.ErrorReporter = app.Services.GetRequiredService<MobileErrorService>();

        // Instanciation eager de MissionViewModel : son constructeur s'abonne à SignalR.
        // Sans ça, l'abonnement ne se fait qu'à la première visite de l'onglet Missions,
        // donc la bannière globale ne fonctionnerait pas sur les autres onglets.
        _ = app.Services.GetRequiredService<MissionViewModel>();

        // Instanciation eager du service de statut de connexion : il s'abonne dès le départ
        // aux changements SignalR/réseau pour que la pastille du header soit toujours à jour.
        _ = app.Services.GetRequiredService<ConnectionStatusService>();

        // Initialise la base SQLite locale et démarre la sync automatique au retour du réseau
        var localDb = app.Services.GetRequiredService<LocalDbService>();
        _ = localDb.InitAsync();

        var syncService = app.Services.GetRequiredService<SyncService>();
        syncService.StartAutoSync();

        return app;
    }
}
