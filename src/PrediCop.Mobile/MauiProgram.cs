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
        builder.Services.AddSingleton<TenantFeaturesService>();
        builder.Services.AddSingleton<GpsTrackingService>();
        builder.Services.AddSingleton(new SignalRService(apiBaseUrl));

        // Offline mode
        builder.Services.AddSingleton<LocalDbService>();
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<SyncService>();

        // Plugin.BLE — BLE beacon vehicle auto-detection
        builder.Services.AddSingleton<IBluetoothLE>(CrossBluetoothLE.Current);
        builder.Services.AddSingleton<IAdapter>(CrossBluetoothLE.Current.Adapter);
        builder.Services.AddSingleton<BleVehicleScanner>();

        // ViewModels
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddTransient<MissionViewModel>();
        builder.Services.AddTransient<PatrolViewModel>();
        builder.Services.AddTransient<PatrolActivationViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<TicketingViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MissionPage>();
        builder.Services.AddTransient<PatrolPage>();
        builder.Services.AddTransient<PatrolActivationPage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<ProfilePage>();
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

        // Initialise la base SQLite locale et démarre la sync automatique au retour du réseau
        var localDb = app.Services.GetRequiredService<LocalDbService>();
        _ = localDb.InitAsync();

        var syncService = app.Services.GetRequiredService<SyncService>();
        syncService.StartAutoSync();

        return app;
    }
}
