namespace RentoomBooking.LiveChat.Services;

public interface ITranslationService
{
    /// <summary>
    /// Translates text to the target language and detects the source language.
    /// </summary>
    /// <param name="text">Text to translate</param>
    /// <param name="targetLanguage">Target language code (e.g., 'pl', 'en')</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Translated text and detected source language</returns>
    Task<TranslationResult> TranslateAsync(string text, string targetLanguage = "pl", CancellationToken ct = default);

    /// <summary>
    /// Detects the language of the text.
    /// </summary>
    /// <param name="text">Text to detect language for</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Detected language code</returns>
    Task<string> DetectLanguageAsync(string text, CancellationToken ct = default);
}

public sealed record TranslationResult(
    string TranslatedText,
    string DetectedLanguage,
    bool WasTranslated);
