using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RentoomBookingWeb.Helpers 
{
    public static class StringExtensions
    {
        public static string ToSlug(this string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return string.Empty;

            string str = phrase.ToLowerInvariant();

            str = RemoveDiacritics(str);

            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");

            str = Regex.Replace(str, @"\s+", " ").Trim();

            str = str.Replace(" ", "-");

            return str;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}