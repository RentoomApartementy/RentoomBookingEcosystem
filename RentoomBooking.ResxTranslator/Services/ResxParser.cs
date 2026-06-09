using System.Xml.Linq;
using ResxTranslator.Models;

namespace ResxTranslator.Services;

public static class ResxParser
{
    /// <summary>
    /// Parse a .resx file into a list of data entries (name → value).
    /// Skips entries with empty or whitespace-only values.
    /// </summary>
    public static List<ResxEntry> Parse(string filePath, bool includeEmpty = false)
    {
        var doc = XDocument.Load(filePath);
        var entries = new List<ResxEntry>();

        foreach (var data in doc.Root!.Elements("data"))
        {
            var name = data.Attribute("name")?.Value;
            var value = data.Element("value")?.Value;
            var comment = data.Element("comment")?.Value;

            if (string.IsNullOrEmpty(name))
                continue;

            if (!includeEmpty && string.IsNullOrWhiteSpace(value))
                continue;

            entries.Add(new ResxEntry(name, value ?? string.Empty, comment));
        }

        return entries;
    }

    /// <summary>
    /// Merge translated entries into an existing .resx file, or create a new one from a template.
    /// Preserves manually-edited entries that are NOT in the update set.
    /// </summary>
    public static void MergeAndSave(
        string targetPath,
        string templatePath,
        Dictionary<string, string> translatedEntries,
        HashSet<string>? keysToRemove = null)
    {
        XDocument doc;

        if (File.Exists(targetPath))
        {
            try
            {
                doc = XDocument.Load(targetPath);
            }
            catch (System.Xml.XmlException)
            {
                // File exists but is empty or corrupt — recreate from template
                doc = XDocument.Load(templatePath);
                doc.Root!.Elements("data").Remove();
            }
        }
        else
        {
            // Create from template, stripping all data entries
            doc = XDocument.Load(templatePath);
            doc.Root!.Elements("data").Remove();
        }

        var root = doc.Root!;
        var existingData = root.Elements("data")
            .ToDictionary(e => e.Attribute("name")?.Value ?? "", e => e);

        // Remove keys that no longer exist in source
        if (keysToRemove != null)
        {
            foreach (var key in keysToRemove)
            {
                if (existingData.TryGetValue(key, out var elem))
                {
                    elem.Remove();
                    existingData.Remove(key);
                }
            }
        }

        // Update existing and add new entries
        foreach (var (name, value) in translatedEntries)
        {
            if (existingData.TryGetValue(name, out var existing))
            {
                existing.Element("value")!.Value = value;
            }
            else
            {
                var newData = new XElement("data",
                    new XAttribute("name", name),
                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement("value", value));
                root.Add(newData);
            }
        }

        doc.Save(targetPath);
    }

    /// <summary>
    /// Get all data entry names from a .resx file.
    /// </summary>
    public static HashSet<string> GetKeys(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var doc = XDocument.Load(filePath);
        return doc.Root!.Elements("data")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => n != null)
            .ToHashSet()!;
    }
}
