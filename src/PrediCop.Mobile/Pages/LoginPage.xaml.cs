using PrediCop.Mobile.Services;

namespace PrediCop.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;

    public LoginPage(AuthService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var success = await _auth.LoginAsync(EmailEntry.Text?.Trim() ?? "", PasswordEntry.Text ?? "");
            if (success)
                await Shell.Current.GoToAsync("//main");
            else
            {
                ErrorLabel.Text = "Identifiants incorrects.";
                ErrorLabel.IsVisible = true;
            }
        }
        catch
        {
            ErrorLabel.Text = "Erreur de connexion au serveur.";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }
}
