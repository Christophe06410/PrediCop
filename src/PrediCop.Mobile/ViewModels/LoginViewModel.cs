using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.ViewModels;

public partial class LoginViewModel(AuthService auth, ILogger<LoginViewModel> log) : ObservableObject
{
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string errorMessage = "";
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private bool isLoading;

    [RelayCommand]
    private async Task LoginAsync()
    {
        HasError = false;
        IsLoading = true;
        log.LogInformation("Login attempt for '{Email}'", Email.Trim());
        try
        {
            var success = await auth.LoginAsync(Email.Trim(), Password);
            if (success)
            {
                log.LogInformation("Login succeeded");
                await Shell.Current.GoToAsync("//main/missions");
            }
            else
            {
                log.LogWarning("Login returned false (bad credentials)");
                ErrorMessage = "Identifiants incorrects.";
                HasError = true;
            }
        }
        catch (HttpRequestException ex)
        {
            log.LogError(ex, "HTTP error during login: {Status} — {Message}",
                ex.StatusCode, ex.Message);
            ErrorMessage = $"Erreur HTTP {(int?)ex.StatusCode}: {ex.Message}";
            HasError = true;
        }
        catch (TaskCanceledException ex)
        {
            log.LogError(ex, "Timeout during login");
            ErrorMessage = "Délai d'attente dépassé (timeout). Vérifiez l'IP/port du serveur.";
            HasError = true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unexpected error during login: {Type} — {Message}",
                ex.GetType().Name, ex.Message);
            ErrorMessage = $"[{ex.GetType().Name}] {ex.Message}";
            HasError = true;
        }
        finally { IsLoading = false; }
    }
}
