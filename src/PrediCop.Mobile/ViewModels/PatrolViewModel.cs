using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PrediCop.Mobile.Messages;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class PatrolViewModel : ObservableObject
{
    private readonly ApiService _api;
    private readonly AuthService _auth;

    public PatrolViewModel(ApiService api, AuthService auth)
    {
        _api = api;
        _auth = auth;
        Streets = [];
    }

    [ObservableProperty] private ObservableCollection<StreetViewModel> _streets;
    [ObservableProperty] private bool _isLoading;

    [RelayCommand]
    public async Task LoadStreetsAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _api.GetAsync<List<StreetViewModel>>("api/streets/priority?count=20");
            Streets = list != null ? new ObservableCollection<StreetViewModel>(list) : [];
        }
        catch { /* show empty state */ }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task MarkPatrolledAsync(Guid streetId)
    {
        try
        {
            await _api.PostAsync($"api/streets/{streetId}/patrol", null);
            await LoadStreetsAsync();
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Erreur", "Impossible d'enregistrer le passage."));
        }
    }

    [RelayCommand]
    private async Task SOSAsync()
    {
        var vehicleId = _auth.VehicleId;
        if (vehicleId == null)
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("SOS", "Aucun véhicule sélectionné. Impossible d'envoyer une alerte SOS."));
            return;
        }

        // Confirmation avant envoi
        WeakReferenceMessenger.Default.Send(
            new SosConfirmationRequest(vehicleId.Value));
    }

    public async Task ConfirmAndSendSOSAsync(Guid vehicleId)
    {
        try
        {
            await _api.PostAsync($"api/vehicles/{vehicleId}/sos", null);
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Alerte SOS", "Alerte SOS envoyée au PC. Les opérateurs ont été notifiés."));
        }
        catch
        {
            WeakReferenceMessenger.Default.Send(
                new AlertMessage("Erreur", "Impossible d'envoyer l'alerte SOS. Vérifiez votre connexion."));
        }
    }
}
