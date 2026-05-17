using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ApiService _api;

    public ProfilePage(ProfileViewModel vm, ApiService api)
    {
        InitializeComponent();
        BindingContext = vm;
        _api = api;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((ProfileViewModel)BindingContext).LoadProfile();
    }

    private void OnGpsToggled(object sender, ToggledEventArgs e)
    {
        if (!e.Value)
            ((ProfileViewModel)BindingContext).StopGps();
    }

    private async void OnChangePasswordClicked(object sender, EventArgs e)
    {
        var current = await DisplayPromptAsync("Mot de passe", "Mot de passe actuel :",
            keyboard: Keyboard.Default, maxLength: 100);
        if (current is null) return;

        var newPwd = await DisplayPromptAsync("Mot de passe", "Nouveau mot de passe (min. 8 caractères) :",
            keyboard: Keyboard.Default, maxLength: 100);
        if (newPwd is null) return;

        if (newPwd.Length < 8)
        {
            await DisplayAlert("Erreur", "Le mot de passe doit faire au moins 8 caractères.", "OK");
            return;
        }

        var confirm = await DisplayPromptAsync("Mot de passe", "Confirmez le nouveau mot de passe :",
            keyboard: Keyboard.Default, maxLength: 100);
        if (confirm is null) return;

        if (confirm != newPwd)
        {
            await DisplayAlert("Erreur", "Les mots de passe ne correspondent pas.", "OK");
            return;
        }

        var (success, error) = await _api.ChangePasswordAsync(current, newPwd);
        if (success)
            await DisplayAlert("Succès", "Mot de passe modifié avec succès.", "OK");
        else
            await DisplayAlert("Erreur", error ?? "Impossible de modifier le mot de passe.", "OK");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Déconnexion", "Se déconnecter ?", "Oui", "Annuler");
        if (!confirm) return;
        await ((ProfileViewModel)BindingContext).LogoutCommand.ExecuteAsync(null);
    }
}
