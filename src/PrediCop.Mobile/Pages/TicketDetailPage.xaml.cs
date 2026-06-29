using PrediCop.Mobile.Services;
using PrediCop.Mobile.ViewModels;

namespace PrediCop.Mobile.Pages;

public partial class TicketDetailPage : ContentPage
{
    private readonly Guid _ticketId;
    private readonly ApiService _api;
    private readonly bool _isToday;

    public TicketDetailPage(Guid ticketId, ApiService api, bool isToday)
    {
        InitializeComponent();
        _ticketId = ticketId;
        _api      = api;
        _isToday  = isToday;

        InfractionPicker.ItemsSource = TicketingViewModel.DefaultInfractionTypes;

        SaveToolbarItem.IsEnabled    = isToday;
        CancelTicketButton.IsVisible = isToday;

        if (!isToday)
        {
            EntryPlate.IsEnabled    = false;
            EntryAddress.IsEnabled  = false;
            InfractionPicker.IsEnabled = false;
            EntryFine.IsEnabled     = false;
            EntryMake.IsEnabled     = false;
            EntryModel.IsEnabled    = false;
            EntryColor.IsEnabled    = false;
            EditorNotes.IsEnabled   = false;
        }
        else
        {
            EntryPlate.Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeCharacter | KeyboardFlags.Suggestions);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        FormFrame.IsVisible        = false;
        TicketNumberLabel.IsVisible = false;
        StatusFrame.IsVisible      = false;

        try
        {
            var ticket = await _api.GetAsync<TicketDetailDto>($"api/tickets/{_ticketId}");
            if (ticket is null) { await DisplayAlert("Erreur", "PV introuvable.", "OK"); return; }

            Title = $"PV {ticket.TicketNumber}";
            TicketNumberLabel.Text    = ticket.TicketNumber;
            TicketNumberLabel.IsVisible = true;
            StatusLabel.Text          = GetStatusLabel(ticket.Status);
            StatusFrame.IsVisible     = true;

            EntryPlate.Text   = ticket.PlateNumber;
            EntryAddress.Text = ticket.IssuedAtAddress;
            EntryFine.Text    = ticket.FineAmount.ToString("F2");
            EntryMake.Text    = ticket.VehicleMake  ?? "";
            EntryModel.Text   = ticket.VehicleModel ?? "";
            EntryColor.Text   = ticket.VehicleColor ?? "";
            EditorNotes.Text  = ticket.Notes ?? "";

            var idx = TicketingViewModel.DefaultInfractionTypes
                .FindIndex(i => i.EnumKey == ticket.InfractionType);
            if (idx >= 0) InfractionPicker.SelectedIndex = idx;

            FormFrame.IsVisible = true;
        }
        catch
        {
            await DisplayAlert("Erreur", "Impossible de charger le PV.", "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void OnPlateTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isToday || e.NewTextValue is null) return;
        var upper = e.NewTextValue.ToUpperInvariant();
        if (upper == e.NewTextValue) return;
        var entry = (Entry)sender;
        int cursor = entry.CursorPosition;
        entry.Text = upper;
        entry.CursorPosition = Math.Min(cursor, upper.Length);
    }

    private void OnInfractionChanged(object sender, EventArgs e)
    {
        if (!_isToday) return;
        if (InfractionPicker.SelectedItem is not InfractionItem item) return;
        if (!decimal.TryParse(EntryFine.Text?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var existing) || existing == 0m)
            EntryFine.Text = item.DefaultFine.ToString("F2");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (!_isToday) return;

        var plate   = EntryPlate.Text?.Trim().ToUpperInvariant() ?? "";
        var address = EntryAddress.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(plate))
        { await DisplayAlert("Erreur", "Plaque requise.", "OK"); return; }

        if (string.IsNullOrWhiteSpace(address))
        { await DisplayAlert("Erreur", "Adresse requise.", "OK"); return; }

        if (!decimal.TryParse(EntryFine.Text?.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var fine) || fine < 0)
        { await DisplayAlert("Erreur", "Montant invalide.", "OK"); return; }

        if (InfractionPicker.SelectedItem is not InfractionItem infraction)
        { await DisplayAlert("Erreur", "Sélectionnez un type d'infraction.", "OK"); return; }

        try
        {
            var body = new
            {
                PlateNumber     = plate,
                IssuedAtAddress = address,
                InfractionType  = infraction.EnumKey,
                FineAmount      = fine,
                VehicleMake     = EntryMake.Text?.Trim(),
                VehicleModel    = EntryModel.Text?.Trim(),
                VehicleColor    = EntryColor.Text?.Trim(),
                Notes           = EditorNotes.Text?.Trim()
            };

            await _api.PutAsync<object>($"api/tickets/{_ticketId}", body);
            await DisplayAlert("Succès", "PV mis à jour.", "OK");
            await Navigation.PopAsync();
        }
        catch
        {
            await DisplayAlert("Erreur", "Impossible de sauvegarder les modifications.", "OK");
        }
    }

    private async void OnCancelTicketClicked(object sender, EventArgs e)
    {
        var reason = await DisplayPromptAsync(
            "Annulation", "Motif d'annulation (optionnel) :", "Confirmer", "Retour", "");
        if (reason is null) return;

        try
        {
            await _api.PutAsync<object>(
                $"api/tickets/{_ticketId}/status",
                new { Status = "Cancelled", Notes = reason.Trim() });
            await DisplayAlert("PV annulé", "Le PV a bien été annulé.", "OK");
            await Navigation.PopAsync();
        }
        catch
        {
            await DisplayAlert("Erreur", "Impossible d'annuler ce PV.", "OK");
        }
    }

    private static string GetStatusLabel(string status) => status switch
    {
        "Issued"    => "Émis",
        "Paid"      => "Payé",
        "Contested" => "Contesté",
        "Cancelled" => "Annulé",
        _           => status
    };

    private class TicketDetailDto
    {
        public Guid Id                { get; set; }
        public string TicketNumber    { get; set; } = "";
        public string PlateNumber     { get; set; } = "";
        public string IssuedAtAddress { get; set; } = "";
        public string InfractionType  { get; set; } = "";
        public decimal FineAmount     { get; set; }
        public string? VehicleMake    { get; set; }
        public string? VehicleModel   { get; set; }
        public string? VehicleColor   { get; set; }
        public string? Notes          { get; set; }
        public string Status          { get; set; } = "";
    }
}
