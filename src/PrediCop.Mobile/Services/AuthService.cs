namespace PrediCop.Mobile.Services;

public class AuthService
{
    private readonly ApiService _api;
    private readonly MediaUploadService _media;
    private const string TokenKey = "auth_token";
    private const string VehicleIdKey = "auth_vehicle_id";

    public string? Token { get; private set; }
    public Guid? VehicleId { get; private set; }
    public UserInfo? CurrentUser { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public AuthService(ApiService api, MediaUploadService media)
    {
        _api = api;
        _media = media;
        Token = Preferences.Get(TokenKey, null);
        var vidStr = Preferences.Get(VehicleIdKey, null);
        if (vidStr != null && Guid.TryParse(vidStr, out var vid))
            VehicleId = vid;
        if (Token != null)
        {
            _api.SetAuthToken(Token);
            _media.SetAuthToken(Token);
        }
    }

    public async Task<List<TenantItem>> GetTenantsAsync()
    {
        try
        {
            var result = await _api.GetAsync<List<TenantItem>>("api/auth/tenants");
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> LoginAsync(string email, string password, string tenantSlug)
    {
        var response = await _api.PostAsync<ApiLoginResponse>("api/auth/login",
            new { email, password, tenantSlug });
        if (response?.AccessToken is null or "") return false;

        Token = response.AccessToken;
        VehicleId = response.User.VehicleId;
        CurrentUser = new UserInfo(
            response.User.Id,
            response.User.FullName,
            response.User.Role,
            response.User.TenantId,
            response.User.TenantName);

        Preferences.Set(TokenKey, Token);
        if (VehicleId.HasValue)
            Preferences.Set(VehicleIdKey, VehicleId.Value.ToString());
        else
            Preferences.Remove(VehicleIdKey);

        _api.SetAuthToken(Token);
        _media.SetAuthToken(Token);
        return true;
    }

    public async Task<(bool Success, string? CallSign)> SelectVehicleAsync(Guid vehicleId)
    {
        var response = await _api.PostAsync<ApiSelectVehicleResponse>(
            $"api/auth/select-vehicle/{vehicleId}", null);
        if (response?.AccessToken is null or "") return (false, null);

        Token = response.AccessToken;
        VehicleId = response.VehicleId;

        Preferences.Set(TokenKey, Token);
        Preferences.Set(VehicleIdKey, VehicleId.Value.ToString());
        _api.SetAuthToken(Token);
        _media.SetAuthToken(Token);
        return (true, response.VehicleCallSign);
    }

    public void Logout()
    {
        Token = null;
        VehicleId = null;
        CurrentUser = null;
        Preferences.Remove(TokenKey);
        Preferences.Remove(VehicleIdKey);
    }

    // Private DTOs matching the API JSON structure exactly
    private class ApiLoginResponse
    {
        public string AccessToken { get; set; } = "";
        public ApiUserDto User { get; set; } = new();
    }

    private class ApiSelectVehicleResponse
    {
        public string AccessToken { get; set; } = "";
        public Guid VehicleId { get; set; }
        public string VehicleCallSign { get; set; } = "";
    }

    private class ApiUserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = "";
        public string BadgeNumber { get; set; } = "";
        public Guid? VehicleId { get; set; }
    }
}

public record UserInfo(Guid Id, string FullName, string Role, Guid TenantId, string TenantName);

public class TenantItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}
