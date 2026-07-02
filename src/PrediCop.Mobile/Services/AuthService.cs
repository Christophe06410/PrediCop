namespace PrediCop.Mobile.Services;

public class AuthService
{
    private readonly ApiService _api;
    private readonly MediaUploadService _media;
    private const string TokenKey              = "auth_token";
    private const string VehicleIdKey          = "auth_vehicle_id";
    private const string VehicleCallSignKey    = "auth_vehicle_callsign";
    private const string VehicleDisplayLabelKey= "auth_vehicle_display_label";
    private const string UserIdKey             = "auth_user_id";
    private const string UserNameKey           = "auth_user_name";
    private const string UserRoleKey           = "auth_user_role";
    private const string UserTenantIdKey       = "auth_user_tenant_id";
    private const string UserTenantNameKey     = "auth_user_tenant_name";

    public string? Token { get; private set; }
    public Guid? VehicleId { get; private set; }
    public string? VehicleCallSign { get; private set; }
    public string? VehicleDisplayLabel { get; private set; }
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

        VehicleCallSign    = Preferences.Get(VehicleCallSignKey, null);
        VehicleDisplayLabel = Preferences.Get(VehicleDisplayLabelKey, null);

        // Restore CurrentUser so the app survives being killed by the OS (e.g. while GPS runs)
        var uidStr     = Preferences.Get(UserIdKey, null);
        var name       = Preferences.Get(UserNameKey, null);
        var role       = Preferences.Get(UserRoleKey, null);
        var tidStr     = Preferences.Get(UserTenantIdKey, null);
        var tenantName = Preferences.Get(UserTenantNameKey, null);
        if (uidStr != null && name != null && role != null && tidStr != null && tenantName != null
            && Guid.TryParse(uidStr, out var uid) && Guid.TryParse(tidStr, out var tid))
        {
            CurrentUser = new UserInfo(uid, name, role, tid, tenantName);
        }

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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] GetTenantsAsync failed: {ex.Message}");
            return [];
        }
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

        Preferences.Set(UserIdKey,       CurrentUser.Id.ToString());
        Preferences.Set(UserNameKey,     CurrentUser.FullName);
        Preferences.Set(UserRoleKey,     CurrentUser.Role);
        Preferences.Set(UserTenantIdKey, CurrentUser.TenantId.ToString());
        Preferences.Set(UserTenantNameKey, CurrentUser.TenantName);

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
        VehicleCallSign = response.VehicleCallSign;
        _api.SetAuthToken(Token);
        _media.SetAuthToken(Token);

        // SharedPreferences.commit() is synchronous disk I/O — await so token is saved before returning
        var t = Token; var vid = VehicleId.Value.ToString(); var cs = VehicleCallSign;
        await Task.Run(() =>
        {
            Preferences.Set(TokenKey, t);
            Preferences.Set(VehicleIdKey, vid);
            Preferences.Set(VehicleCallSignKey, cs);
        });

        return (true, response.VehicleCallSign);
    }

    public void SetVehicleDisplayLabel(string label)
    {
        VehicleDisplayLabel = label;
        _ = Task.Run(() => Preferences.Set(VehicleDisplayLabelKey, label));
    }

    public void Logout()
    {
        Token = null;
        VehicleId = null;
        VehicleCallSign = null;
        VehicleDisplayLabel = null;
        CurrentUser = null;
        Preferences.Remove(TokenKey);
        Preferences.Remove(VehicleIdKey);
        Preferences.Remove(VehicleCallSignKey);
        Preferences.Remove(VehicleDisplayLabelKey);
        Preferences.Remove(UserIdKey);
        Preferences.Remove(UserNameKey);
        Preferences.Remove(UserRoleKey);
        Preferences.Remove(UserTenantIdKey);
        Preferences.Remove(UserTenantNameKey);
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
