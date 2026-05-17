namespace PrediCop.Mobile.Services;

public class AuthService
{
    private readonly ApiService _api;
    private readonly MediaUploadService _media;
    private const string TokenKey = "auth_token";

    public string? Token { get; private set; }
    public UserInfo? CurrentUser { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public AuthService(ApiService api, MediaUploadService media)
    {
        _api = api;
        _media = media;
        Token = Preferences.Get(TokenKey, null);
        if (Token != null)
        {
            _api.SetAuthToken(Token);
            _media.SetAuthToken(Token);
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var response = await _api.PostAsync<ApiLoginResponse>("api/auth/login", new { email, password });
        if (response?.AccessToken is null or "") return false;

        Token = response.AccessToken;
        CurrentUser = new UserInfo(
            response.User.Id,
            response.User.FullName,
            response.User.Role,
            response.User.TenantId);

        Preferences.Set(TokenKey, Token);
        _api.SetAuthToken(Token);
        _media.SetAuthToken(Token);
        return true;
    }

    public void Logout()
    {
        Token = null;
        CurrentUser = null;
        Preferences.Remove(TokenKey);
    }

    // Private DTOs matching the API JSON structure exactly
    private class ApiLoginResponse
    {
        public string AccessToken { get; set; } = "";
        public ApiUserDto User { get; set; } = new();
    }

    private class ApiUserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public Guid TenantId { get; set; }
        public string BadgeNumber { get; set; } = "";
    }
}

public record UserInfo(Guid Id, string FullName, string Role, Guid TenantId);
