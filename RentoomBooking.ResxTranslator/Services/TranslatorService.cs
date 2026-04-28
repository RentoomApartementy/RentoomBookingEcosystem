using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResxTranslator.Services;

/// <summary>
/// Azure AI Translator REST API v3.0 client.
/// Supports batch translation (up to 100 elements / request, 50,000 chars).
/// </summary>
public sealed class TranslatorService : IDisposable
{
    private const string Endpoint = "https://api.cognitive.microsofttranslator.com";
    private const int MaxBatchSize = 100;
    private const int MaxCharsPerRequest = 49_000; // leave margin from 50k limit

    private readonly HttpClient _http;
    private readonly string _subscriptionKey;
    private readonly string _region;

    public TranslatorService(string subscriptionKey, string region)
    {
        _subscriptionKey = subscriptionKey;
        _region = region;
        _http = new HttpClient();
    }

    /// <summary>
    /// Translate a batch of texts from source language to one or more target languages.
    /// Handles chunking for API limits automatically.
    /// Returns: dictionary[targetLang] → dictionary[originalText] → translatedText
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, string>>> TranslateBatchAsync(
        string sourceLanguage,
        string[] targetLanguages,
        List<string> texts)
    {
        var result = targetLanguages.ToDictionary(
            lang => lang,
            _ => new Dictionary<string, string>());

        if (texts.Count == 0)
            return result;

        var chunks = ChunkTexts(texts);

        var targetParams = string.Join("&", targetLanguages.Select(l => $"to={l}"));
        var url = $"{Endpoint}/translate?api-version=3.0&from={sourceLanguage}&{targetParams}";

        foreach (var chunk in chunks)
        {
            var body = chunk.Select(t => new { Text = t }).ToArray();
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
            request.Headers.Add("Ocp-Apim-Subscription-Region", _region);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(
                    $"Azure Translator API error ({response.StatusCode}): {error}");
            }

            var translations = await response.Content.ReadFromJsonAsync<TranslationResponse[]>();

            if (translations == null)
                continue;

            for (var i = 0; i < translations.Length && i < chunk.Count; i++)
            {
                var originalText = chunk[i];
                foreach (var translation in translations[i].Translations)
                {
                    if (result.TryGetValue(translation.To, out var langDict))
                        langDict[originalText] = translation.Text;
                }
            }
        }

        return result;
    }

    private static List<List<string>> ChunkTexts(List<string> texts)
    {
        var chunks = new List<List<string>>();
        var currentChunk = new List<string>();
        var currentChars = 0;

        foreach (var text in texts)
        {
            if (currentChunk.Count >= MaxBatchSize ||
                (currentChars + text.Length > MaxCharsPerRequest && currentChunk.Count > 0))
            {
                chunks.Add(currentChunk);
                currentChunk = new List<string>();
                currentChars = 0;
            }

            currentChunk.Add(text);
            currentChars += text.Length;
        }

        if (currentChunk.Count > 0)
            chunks.Add(currentChunk);

        return chunks;
    }

    public void Dispose() => _http.Dispose();

    private sealed class TranslationResponse
    {
        [JsonPropertyName("translations")]
        public TranslationItem[] Translations { get; set; } = [];
    }

    private sealed class TranslationItem
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("to")]
        public string To { get; set; } = "";
    }
}
