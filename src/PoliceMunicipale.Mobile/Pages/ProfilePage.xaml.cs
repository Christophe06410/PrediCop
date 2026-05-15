using PoliceMunicipale.Mobile.Services;

namespace PoliceMunicipale.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GpsTrackingService _gps;

    public ProfilePage(AuthService auth, GpsTrackingService gps)
    {
        InitializeComponent();
        _auth = auth;
        _gps = gps;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_auth.CurrentUser != null)
        {
            UserNameLabel.Text = _auth.CurrentUser.FullName;
            BadgeLabel.Text = $"Rôle: {_auth.CurrentUser.Role}";
        }
    }

    private void OnGpsToggled(object sender, ToggledEventArgs e)
    {
        if (!e.Value) _gps.Stop();
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Déconnexion", "Se déconnecter ?", "Oui", "Annuler");
        if (!confirm) return;
        _auth.Logout();
        _gps.Stop();
        await Shell.Current.GoToAsync("//login");
    }
}
