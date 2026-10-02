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
            await DisplayAlertAsync(m.Title, m.Text, "OK"));
        WeakReferenceMessenger.Default.Register<SosConfirmationRequest>(this, async (_, req) =>
        {
            var vm = (PatrolViewModel)BindingContext;
            var confirmed = await DisplayAlertAsync(
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

    private void OnPatrolledClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid streetId)
            ((PatrolViewModel)BindingContext).MarkPatrolledCommand.Execute(streetId);
    }

    private void OnViewOnMapClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not StreetViewModel street) return;
        MapPage.PendingFocusFromPatrol = (street.CenterLatitude, street.CenterLongitude, street.Name);
        AppShell.SwitchToTab("map");
    }

    private async void OnDirectionsClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not StreetViewModel street) return;
        try
        {
            var location = new Location(street.CenterLatitude, street.CenterLongitude);
            var options = new MapLaunchOptions { Name = street.Name };
            await Map.Default.OpenAsync(location, options);
        }
        catch { await DisplayAlertAsync("Erreur", "Impossible d'ouvrir l'application GPS.", "OK"); }
    }
}
