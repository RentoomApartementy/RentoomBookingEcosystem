using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
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
    private const int MaxBatchSize = 1;
    private const int MaxCharsPerRequest = 25_000; // conservative: accounts for JSON escaping overhead
    private const int MaxTargetLanguagesPerRequest = 10; // avoid request-size overflows with many targets

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Preserve non-ASCII characters (e.g. Polish ą, ę) as-is instead of escaping to \uXXXX,
        // which would multiply byte size 3-6× and trigger the 50k limit.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

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

        var textChunks = ChunkTexts(texts);
        var langChunks = ChunkLanguages(targetLanguages);

        var totalRequests = textChunks.Count * langChunks.Count;
        var requestsDone = 0;

        foreach (var langChunk in langChunks)
        {
            var targetParams = string.Join("&", langChunk.Select(l => $"to={l}"));
            var url = $"{Endpoint}/translate?api-version=3.0&from={sourceLanguage}&{targetParams}";

            foreach (var chunk in textChunks)
            {
                requestsDone++;
                Console.Write($"\r  Translating... {requestsDone}/{totalRequests} requests");

                var body = chunk.Select(t => new { Text = t }).ToArray();
                var json = JsonSerializer.Serialize(body, JsonOptions);
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
        }

        Console.WriteLine($"\r  Translating... {totalRequests}/{totalRequests} requests — done.");

        return result;
    }

    private static List<string[]> ChunkLanguages(string[] languages) =>
        languages
            .Select((lang, i) => (lang, i))
            .GroupBy(x => x.i / MaxTargetLanguagesPerRequest)
            .Select(g => g.Select(x => x.lang).ToArray())
            .ToList();

    private static List<List<string>> ChunkTexts(List<string> texts)
    {
        var chunks = new List<List<string>>();
        var currentChunk = new List<string>();
        var currentBytes = 0;

        foreach (var text in texts)
        {
            var textBytes = Encoding.UTF8.GetByteCount(text);
            if (currentChunk.Count >= MaxBatchSize ||
                (currentBytes + textBytes > MaxCharsPerRequest && currentChunk.Count > 0))
            {
                chunks.Add(currentChunk);
                currentChunk = new List<string>();
                currentBytes = 0;
            }

            currentChunk.Add(text);
            currentBytes += textBytes;
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
