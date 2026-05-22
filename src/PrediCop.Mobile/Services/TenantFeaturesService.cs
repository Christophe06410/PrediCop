namespace PrediCop.Mobile.Services;

public class TenantFeaturesService(ApiService api)
{
    public TenantMobileFeatures Current { get; private set; } = TenantMobileFeatures.Default;

    public async Task LoadAsync()
    {
        try
        {
            var f = await api.GetAsync<TenantMobileFeatures>("/api/tenant/features");
            Current = f ?? TenantMobileFeatures.Default;
        }
        catch
        {
            Current = TenantMobileFeatures.Default;
        }
    }

    public void Clear() => Current = TenantMobileFeatures.Default;
}

public class TenantMobileFeatures
{
    public bool ModuleVerbalisationEnabled { get; set; } = false;
    public bool GpsTrackingEnabled { get; set; } = true;
    public bool GeofencingEnabled { get; set; } = false;
    public bool PhotoAttachmentsEnabled { get; set; } = true;

    public static TenantMobileFeatures Default => new();
}
