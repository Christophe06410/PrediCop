namespace PrediCop.Mobile.Services;

public class AuthService
{
    private readonly ApiService _api;
    private const string TokenKey = "auth_token";
    private const string UserKey = "auth_user";

    public string? Token { get; private set; }
    public UserInfo? CurrentUser { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public AuthService(ApiService api)
    {
        _api = api;
        Token = Preferences.Get(TokenKey, null);
        if (Token != null)
            _api.SetAuthToken(Token);
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await _api.PostAsync<LoginResponse>("api/auth/login", new { email, password });
        if (response == null) return false;

        Token = response.Token;
        CurrentUser = new UserInfo(response.UserId, response.FullName, response.Role);
        Preferences.Set(TokenKey, Token);
        _api.SetAuthToken(Token);
        return true;
    }

    public void Logout()
    {
        Token = null;
        CurrentUser = null;
        Preferences.Remove(TokenKey);
    }
}

public record LoginResponse(string Token, Guid UserId, string FullName, string Role);
public record UserInfo(Guid Id, string FullName, string Role);
