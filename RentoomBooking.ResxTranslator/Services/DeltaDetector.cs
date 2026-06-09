using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ResxTranslator.Models;

namespace ResxTranslator.Services;

/// <summary>
/// Tracks SHA256 hashes of source .resx values to detect changes between runs.
/// Stores hashes in a .resx-hashes.json file inside the translator project so the
/// cache travels with the project and can be committed alongside the .resx files.
/// </summary>
public sealed class DeltaDetector
{
    private readonly string _hashFilePath;
    private Dictionary<string, Dictionary<string, string>> _hashes; // file -> (key -> hash)

    public DeltaDetector(string repoRoot)
    {
        _hashFilePath = Path.Combine(repoRoot, "RentoomBooking.ResxTranslator", ".resx-hashes.json");
        _hashes = LoadHashes();
    }

    /// <summary>
    /// Compare source entries against cached hashes.
    /// Returns entries that are new or changed (need translation).
    /// </summary>
    public List<ResxEntry> GetChangedEntries(string sourceFilePath, List<ResxEntry> sourceEntries, bool forceAll)
    {
        if (forceAll)
            return sourceEntries;

        var fileKey = NormalizeFilePath(sourceFilePath);

        if (!_hashes.TryGetValue(fileKey, out var cachedHashes))
            return sourceEntries; // all new

        var changed = new List<ResxEntry>();
        foreach (var entry in sourceEntries)
        {
            var currentHash = ComputeHash(entry.Value);
            if (!cachedHashes.TryGetValue(entry.Name, out var cachedHash) || cachedHash != currentHash)
                changed.Add(entry);
        }

        return changed;
    }

    /// <summary>
    /// Get keys that exist in cached hashes but not in current source (deleted keys).
    /// </summary>
    public List<string> GetRemovedKeys(string sourceFilePath, List<ResxEntry> currentEntries)
    {
        var fileKey = NormalizeFilePath(sourceFilePath);
        if (!_hashes.TryGetValue(fileKey, out var cachedHashes))
            return [];

        var currentKeys = currentEntries.Select(e => e.Name).ToHashSet();
        return cachedHashes.Keys.Where(k => !currentKeys.Contains(k)).ToList();
    }

    /// <summary>
    /// Update hash cache after successful translation.
    /// </summary>
    public void UpdateHashes(string sourceFilePath, List<ResxEntry> entries)
    {
        var fileKey = NormalizeFilePath(sourceFilePath);
        _hashes[fileKey] = entries.ToDictionary(
            e => e.Name,
            e => ComputeHash(e.Value));
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(_hashFilePath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(_hashes, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_hashFilePath, json);
    }

    private Dictionary<string, Dictionary<string, string>> LoadHashes()
    {
        if (!File.Exists(_hashFilePath))
            return new();

        var json = File.ReadAllText(_hashFilePath);
        return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) ?? new();
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private string NormalizeFilePath(string filePath)
    {
        // Store relative path with forward slashes for cross-platform
        if (filePath.StartsWith(_hashFilePath))
            return filePath;

        return filePath.Replace('\\', '/');
    }
}
