using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private readonly BleVehicleScanner? _bleScanner;

    public ProfilePage(ProfileViewModel vm, BleVehicleScanner? bleScanner = null)
    {
        InitializeComponent();
        _vm = vm;
        _bleScanner = bleScanner;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadProfile();
    }

    private void OnGpsToggled(object sender, ToggledEventArgs e)
    {
        if (!e.Value) _vm.StopGps();
    }

    private async void OnDetectVehicleClicked(object sender, EventArgs e)
    {
        if (_bleScanner is null)
        {
            await DisplayAlert("Non disponible", "La détection BLE n'est pas disponible sur cette plateforme.", "OK");
            return;
        }

        BtnDetectVehicle.IsEnabled = false;
        BtnDetectVehicle.Text = "Scan en cours…";

        try
        {
            var callSign = await _bleScanner.ScanAndAssignAsync();
            if (callSign is not null)
            {
                _vm.CurrentVehicle = callSign;
                await DisplayAlert("Véhicule détecté", $"Véhicule {callSign} détecté et assigné automatiquement.", "OK");
            }
            else
            {
                await DisplayAlert(
                    "Aucun véhicule détecté",
                    "Aucun beacon BLE reconnu à proximité. Vérifiez que le Bluetooth est activé et que vous êtes bien dans le véhicule, ou sélectionnez manuellement.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", $"Erreur lors du scan BLE : {ex.Message}", "OK");
        }
        finally
        {
            BtnDetectVehicle.IsEnabled = true;
            BtnDetectVehicle.Text = "Détecter mon véhicule (BLE)";
        }
    }

    private async void OnSelectVehicleClicked(object sender, EventArgs e)
    {
        var vehicles = await _vm.LoadVehiclesAsync();
        if (vehicles.Count == 0)
        {
            await DisplayAlert("Véhicules", "Aucun véhicule disponible.", "OK");
            return;
        }

        var labels = vehicles.Select(v => v.IsCurrent ? $"✓ {v.Label}" : v.Label).ToArray();
        var selected = await DisplayActionSheet("Sélectionner votre véhicule", "Annuler", null, labels);
        if (selected == null || selected == "Annuler") return;

        var item = vehicles.FirstOrDefault(v =>
            selected == v.Label || selected == $"✓ {v.Label}");
        if (item == null) return;

        var success = await _vm.SelectVehicleAsync(item.Id, item.Label);
        if (success)
            await DisplayAlert("Véhicule", $"Véhicule {item.Label} sélectionné.", "OK");
        else
            await DisplayAlert("Erreur", "Impossible de sélectionner ce véhicule.", "OK");
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

        var api = Handler?.MauiContext?.Services.GetService<ApiService>();
        if (api == null) return;
        var (success, error) = await api.ChangePasswordAsync(current, newPwd);
        if (success)
            await DisplayAlert("Succès", "Mot de passe modifié avec succès.", "OK");
        else
            await DisplayAlert("Erreur", error ?? "Impossible de modifier le mot de passe.", "OK");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Déconnexion", "Se déconnecter ?", "Oui", "Annuler");
        if (!confirm) return;
        await _vm.LogoutCommand.ExecuteAsync(null);
    }
}
