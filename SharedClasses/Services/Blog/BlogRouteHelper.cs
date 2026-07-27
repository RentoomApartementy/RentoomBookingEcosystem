using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RentoomBooking.SharedClasses.Services.Blog;

public static class BlogRouteHelper
{
    public const string DefaultCategorySlug = "inne";

    public static string GetCategorySlug(string? category)
    {
        var slug = ToSlug(category);
        return string.IsNullOrWhiteSpace(slug) ? DefaultCategorySlug : slug;
    }

    private static string ToSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ToLowerInvariant().Replace("ł", "l").Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var withoutDiacritics = builder.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(Regex.Replace(withoutDiacritics, @"[^a-z0-9\s-]", string.Empty), @"\s+", " ")
            .Trim()
            .Replace(" ", "-");
    }
}
