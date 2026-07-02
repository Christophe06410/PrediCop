using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PrediCop.Mobile.Services;

/// <summary>
/// Singleton qui gère l'état de l'alerte mission globale (bannière visible sur tous les écrans).
/// </summary>
public class MissionAlertService : INotifyPropertyChanged
{
    private bool _hasAlert;
    private string _alertTitle = "";
    private string _alertAddress = "";

    public bool HasAlert
    {
        get => _hasAlert;
        private set { _hasAlert = value; OnPropertyChanged(); }
    }

    public string AlertTitle
    {
        get => _alertTitle;
        private set { _alertTitle = value; OnPropertyChanged(); }
    }

    public string AlertAddress
    {
        get => _alertAddress;
        private set { _alertAddress = value; OnPropertyChanged(); }
    }

    public void Show(string title, string address)
    {
        AlertTitle = title;
        AlertAddress = address;
        HasAlert = true;
    }

    public void Dismiss()
    {
        HasAlert = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
