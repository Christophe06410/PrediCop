#if DEBUG
namespace PrediCop.Mobile.Services;

public static class MobileLogger
{
    private static ApiService? _api;

    public static void Init(ApiService api) => _api = api;

    public static void Log(string tag, string message) =>
        _api?.PostAsync("api/log", new { tag, message })
            .ContinueWith(_ => { });
}
#endif
