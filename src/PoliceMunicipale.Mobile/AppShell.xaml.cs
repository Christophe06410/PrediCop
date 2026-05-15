using PoliceMunicipale.Mobile.Pages;
using PoliceMunicipale.Mobile.Services;

namespace PoliceMunicipale.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("main", typeof(MainPage));
        Routing.RegisterRoute("missions", typeof(MissionPage));
        Routing.RegisterRoute("patrol", typeof(PatrolPage));
        Routing.RegisterRoute("map", typeof(MapPage));
        Routing.RegisterRoute("profile", typeof(ProfilePage));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var auth = Handler?.MauiContext?.Services.GetService<AuthService>();
        if (auth?.IsLoggedIn == true)
            await GoToAsync("//main");
        else
            await GoToAsync("//login");
    }
}
