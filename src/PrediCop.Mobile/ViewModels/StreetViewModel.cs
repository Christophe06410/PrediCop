using System.Text.Json.Serialization;

namespace PrediCop.Mobile.ViewModels;

public class StreetViewModel
{
    [JsonPropertyName("id")]
    public Guid StreetId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("currentRiskScore")]
    public int RiskScore { get; set; }

    [JsonPropertyName("lastPatrolledAt")]
    public DateTime? LastPatrolledAt { get; set; }

    [JsonPropertyName("startLatitude")]
    public double StartLatitude { get; set; }

    [JsonPropertyName("startLongitude")]
    public double StartLongitude { get; set; }

    [JsonPropertyName("endLatitude")]
    public double EndLatitude { get; set; }

    [JsonPropertyName("endLongitude")]
    public double EndLongitude { get; set; }

    public double CenterLatitude => (StartLatitude + EndLatitude) / 2;
    public double CenterLongitude => (StartLongitude + EndLongitude) / 2;

    public string LastPatrolText => LastPatrolledAt.HasValue
        ? $"Dernière patrouille: {LastPatrolledAt.Value:dd/MM HH:mm}"
        : "Jamais patrouillée";

    public Color RiskColor => RiskScore switch
    {
        > 70 => Color.FromArgb("#ef4444"),
        > 40 => Color.FromArgb("#f59e0b"),
        > 20 => Color.FromArgb("#eab308"),
        _ => Color.FromArgb("#22c55e")
    };
}
