namespace ResxTranslator.Models;

public sealed record TranslationTask(
    string SourceFilePath,
    string TargetFilePath,
    string TargetCulture,
    List<ResxEntry> KeysToTranslate,
    List<string> KeysToRemove);
