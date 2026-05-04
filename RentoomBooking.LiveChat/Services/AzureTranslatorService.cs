using System.Text.RegularExpressions;
using Azure;
using Azure.AI.Translation.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RentoomBooking.LiveChat.Services;

public sealed class AzureTranslatorService : ITranslationService
{
    private readonly TextTranslationClient _client;
    private readonly string _defaultTargetLanguage;
    private readonly ILogger<AzureTranslatorService> _logger;

    public AzureTranslatorService(
        TextTranslationClient client,
        IOptions<AzureTranslatorOptions> options,
        ILogger<AzureTranslatorService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _defaultTargetLanguage = options?.Value.DefaultTargetLanguage ?? "pl";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TranslationResult> TranslateAsync(
        string text,
        string targetLanguage = "pl",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        targetLanguage = string.IsNullOrWhiteSpace(targetLanguage) ? _defaultTargetLanguage : targetLanguage;

        try
        {
            var response = await _client.TranslateAsync(
                text: text,
                targetLanguage: targetLanguage,
                cancellationToken: ct);

            if (response.Value.Count == 0)
                return new TranslationResult(text, "unknown", false);

            var translation = response.Value.First();
            var detectedLanguage = translation.DetectedLanguage?.Language ?? "unknown";
            var translatedText = translation.Translations.First().Text;

            var wasTranslated = !string.Equals(
                detectedLanguage,
                targetLanguage,
                StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Translated text from {SourceLanguage} to {TargetLanguage}",
                detectedLanguage,
                targetLanguage);

            return new TranslationResult(translatedText, detectedLanguage, wasTranslated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate text. Returning original text.");
            return new TranslationResult(text, "unknown", false);
        }
    }

    public async Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "unknown";

        try
        {
            var response = await _client.TranslateAsync(
                text: text,
                targetLanguage: _defaultTargetLanguage,
                cancellationToken: ct);

            if (response.Value.Count == 0)
                return "unknown";

            return response.Value.First().DetectedLanguage?.Language ?? "unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect language. Returning 'unknown'.");
            return "unknown";
        }
    }
}
