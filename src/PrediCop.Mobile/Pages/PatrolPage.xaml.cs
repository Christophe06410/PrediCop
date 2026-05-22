using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using PrediCop.Mobile.Messages;
using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class PatrolPage : ContentPage
{
    public PatrolPage(PatrolViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((PatrolViewModel)BindingContext).LoadStreetsCommand.Execute(null);
        WeakReferenceMessenger.Default.Register<AlertMessage>(this, async (_, m) =>
            await DisplayAlert(m.Title, m.Text, "OK"));
        WeakReferenceMessenger.Default.Register<SosConfirmationRequest>(this, async (_, req) =>
        {
            var vm = (PatrolViewModel)BindingContext;
            var confirmed = await DisplayAlert(
                "🆘 Alerte SOS",
                "Êtes-vous en danger ? Envoyer une alerte SOS à tous les opérateurs ?",
                "Envoyer SOS", "Annuler");
            if (confirmed)
                await vm.ConfirmAndSendSOSAsync(req.VehicleId);
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void OnPatrolledClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid streetId)
            ((PatrolViewModel)BindingContext).MarkPatrolledCommand.Execute(streetId);
    }

    private async void OnViewOnMapClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not StreetViewModel street) return;
        var lat = street.CenterLatitude.ToString("F6", CultureInfo.InvariantCulture);
        var lng = street.CenterLongitude.ToString("F6", CultureInfo.InvariantCulture);
        var name = Uri.EscapeDataString(street.Name);
        await Shell.Current.GoToAsync($"//main/map?centerLat={lat}&centerLng={lng}&markerName={name}");
    }

    private async void OnDirectionsClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not StreetViewModel street) return;
        try
        {
            var location = new Location(street.CenterLatitude, street.CenterLongitude);
            var options = new MapLaunchOptions { Name = street.Name };
            await Map.Default.OpenAsync(location, options);
        }
        catch { await DisplayAlert("Erreur", "Impossible d'ouvrir l'application GPS.", "OK"); }
    }
}
