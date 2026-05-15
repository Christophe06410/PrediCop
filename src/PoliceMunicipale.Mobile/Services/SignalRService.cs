using Microsoft.AspNetCore.SignalR.Client;

namespace PoliceMunicipale.Mobile.Services;

public class SignalRService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;

    public event EventHandler<MissionProposedArgs>? MissionProposed;
    public event EventHandler<string>? MissionStatusChanged;
    public event EventHandler<StreetRiskArgs>? StreetRiskUpdated;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public SignalRService(string baseUrl)
    {
        _hubUrl = $"{baseUrl}/hubs/police";
    }

    public async Task ConnectAsync(string token, Guid vehicleId)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => options.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<object>("MissionProposed", data =>
            MissionProposed?.Invoke(this, new MissionProposedArgs(data)));

        _connection.On<object>("MissionStatusChanged", data =>
            MissionStatusChanged?.Invoke(this, data?.ToString() ?? ""));

        _connection.On<object>("StreetRiskUpdated", data =>
            StreetRiskUpdated?.Invoke(this, new StreetRiskArgs(data)));

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinVehicleGroup", vehicleId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
            await _connection.DisposeAsync();
    }
}

public record MissionProposedArgs(object Data);
public record StreetRiskArgs(object Data);
