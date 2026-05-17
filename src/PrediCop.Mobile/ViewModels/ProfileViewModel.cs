using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class ProfileViewModel(AuthService auth, GpsTrackingService gps) : ObservableObject
{
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string badge = "";

    public void LoadProfile()
    {
        if (auth.CurrentUser == null) return;
        UserName = auth.CurrentUser.FullName;
        Badge = $"Rôle: {auth.CurrentUser.Role}";
    }

    public void StopGps() => gps.Stop();

    [RelayCommand]
    private async Task LogoutAsync()
    {
        auth.Logout();
        gps.Stop();
        await Shell.Current.GoToAsync("//login");

    }
}
