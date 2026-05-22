using System.Net.Http.Json;
using System.Text.Json;

namespace PrediCop.BackOffice.Services;

public class TextAssistantService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<TextAssistantService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<string> CorrectSpellingAsync(string text, CancellationToken ct)
    {
        var prompt =
            "Tu es un assistant de correction orthographique pour une application de Police Municipale française. " +
            "Corrige uniquement les fautes d'orthographe, de grammaire et de conjugaison dans le texte ci-dessous. " +
            "Ne change pas le sens, le style ni le vocabulaire. " +
            "Réponds UNIQUEMENT avec le texte corrigé, sans commentaire, sans guillemets, sans balise.\n\n" +
            text;

        return (await CallAsync(prompt, 1024, ct)).Trim();
    }

    public async Task<List<TextSuggestion>> ImproveAsync(string text, CancellationToken ct)
    {
        var prompt =
            "Tu es un assistant rédactionnel pour une application de Police Municipale française. " +
            "Propose des améliorations de tournures de phrases et corrige les fautes dans le texte ci-dessous. " +
            "Réponds UNIQUEMENT avec un JSON valide (sans markdown, sans backticks) dans ce format exact :\n" +
            "{\"suggestions\":[{\"original\":\"texte exact à remplacer\",\"suggested\":\"remplacement proposé\",\"reason\":\"explication courte\"}]}\n" +
            "Les valeurs \"original\" doivent être des sous-chaînes exactes du texte source. " +
            "Si aucune amélioration n'est nécessaire, retourne {\"suggestions\":[]}.\n\n" +
            text;

        var raw = (await CallAsync(prompt, 2048, ct)).Trim();

        // Strip accidental markdown fences
        if (raw.StartsWith("```")) raw = raw.Split('\n', 2).Skip(1).First().TrimEnd('`').Trim();

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var arr = doc.RootElement.GetProperty("suggestions");
            var result = new List<TextSuggestion>();

            foreach (var s in arr.EnumerateArray())
            {
                var original  = s.GetProperty("original").GetString() ?? "";
                var suggested = s.GetProperty("suggested").GetString() ?? "";
                var reason    = s.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";

                if (!string.IsNullOrEmpty(original) && text.Contains(original, StringComparison.Ordinal))
                    result.Add(new TextSuggestion(original, suggested, reason));
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse improve response: {Raw}", raw);
            return [];
        }
    }

    private async Task<string> CallAsync(string userPrompt, int maxTokens, CancellationToken ct)
    {
        var apiKey = config["Ai:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Clé API IA non configurée (Ai:ApiKey).");

        var model = config["Ai:Model"] ?? "claude-haiku-4-5-20251001";

        var client = httpClientFactory.CreateClient("Anthropic");

        var body = new
        {
            model,
            max_tokens = maxTokens,
            messages = new[] { new { role = "user", content = userPrompt } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Content = JsonContent.Create(body);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Anthropic API error {Status}: {Body}", (int)response.StatusCode, err);
            throw new HttpRequestException($"Anthropic API a retourné HTTP {(int)response.StatusCode}.");
        }

        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOpts, ct);
        return result?.Content?.FirstOrDefault()?.Text ?? "";
    }

    private class AnthropicResponse
    {
        public List<AnthropicContent> Content { get; set; } = [];
    }

    private class AnthropicContent
    {
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
    }
}

public record TextSuggestion(string Original, string Suggested, string Reason);
