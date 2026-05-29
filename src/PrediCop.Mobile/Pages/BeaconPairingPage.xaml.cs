using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class BeaconPairingPage : ContentPage
{
    private readonly BeaconPairingViewModel _vm;

    public BeaconPairingPage(BeaconPairingViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadVehiclesAsync();
    }

    private async void OnAssociateClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not DiscoveredBeacon beacon)
            return;

        if (_vm.SelectedVehicle is null)
        {
            await DisplayAlert("Véhicule requis", "Sélectionnez d'abord un véhicule dans la liste.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Confirmer l'association",
            $"Associer le beacon\n\n{beacon.Uuid}\n\nau véhicule {_vm.SelectedVehicle.CallSign} ?",
            "Associer", "Annuler");

        if (!confirm) return;

        var success = await _vm.AssociateBeaconAsync(beacon);
        if (success)
            await DisplayAlert("Succès",
                $"Le beacon est maintenant associé au véhicule {_vm.SelectedVehicle.CallSign}.\n" +
                "La détection automatique BLE fonctionnera au prochain démarrage.",
                "OK");
        else
            await DisplayAlert("Erreur", "Impossible d'enregistrer l'association. Vérifiez la connexion réseau.", "OK");
    }
}
