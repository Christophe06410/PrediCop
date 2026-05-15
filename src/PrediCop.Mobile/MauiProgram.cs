using Microsoft.Extensions.Logging;
using PrediCop.Mobile.Pages;
using PrediCop.Mobile.Services;

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

        var apiBaseUrl = "https://localhost:7001";

        builder.Services.AddSingleton(sp =>
        {
            var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl) };
            return new ApiService(http);
        });

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<GpsTrackingService>();
        builder.Services.AddSingleton(new SignalRService(apiBaseUrl));

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MissionPage>();
        builder.Services.AddTransient<PatrolPage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
