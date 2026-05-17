using Microsoft.Extensions.Logging;
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

#if WINDOWS
        var apiBaseUrl = "https://localhost:7229";
#else
        var apiBaseUrl = "https://10.164.73.59:7229";
#endif

        var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

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
        builder.Services.AddSingleton<GpsTrackingService>();
        builder.Services.AddSingleton(new SignalRService(apiBaseUrl));

        // ViewModels
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<MissionViewModel>();
        builder.Services.AddTransient<PatrolViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MissionPage>();
        builder.Services.AddTransient<PatrolPage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<ProfilePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
