using System.Globalization;
using RentoomBooking.SharedClasses.Models.Database.EFEntitites;

namespace RentoomBooking.SharedClasses.Services.ApartmentMedia
{
    /// <summary>
    /// Culture matching for <see cref="ApartmentMediaAltText"/> rows. The repository stores cultures
    /// inconsistently (cookie notices use "pl-PL"/"en-US", customer terms use "pl"/"en"), so the same
    /// tolerant fallback chain as CookieConsentRepository.SelectTranslation is used here.
    /// </summary>
    public static class AltTextCultureResolver
    {
        public const string DefaultCulture = "pl-PL";

        public static string NormalizeCulture(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return DefaultCulture;
            }

            try
            {
                return CultureInfo.GetCultureInfo(cultureName).Name;
            }
            catch (CultureNotFoundException)
            {
                return DefaultCulture;
            }
        }

        /// <summary>
        /// Neutral part of a culture name ("pl-PL" -> "pl").
        /// </summary>
        public static string GetNeutralCulture(string culture) => culture.Split('-')[0];

        /// <summary>
        /// Neutral cultures worth loading from the database for the requested culture. Used to keep the
        /// SQL filter narrow (a specific match, a neutral match and the default culture) while still
        /// letting <see cref="SelectBest"/> apply the full fallback chain in memory.
        /// </summary>
        public static IReadOnlyList<string> GetCandidateNeutralCultures(string normalizedCulture)
        {
            var neutral = GetNeutralCulture(normalizedCulture);
            var neutralDefault = GetNeutralCulture(DefaultCulture);

            return string.Equals(neutral, neutralDefault, StringComparison.OrdinalIgnoreCase)
                ? new[] { neutral }
                : new[] { neutral, neutralDefault };
        }

        public static ApartmentMediaAltText? SelectBest(IEnumerable<ApartmentMediaAltText> altTexts, string normalizedCulture)
        {
            var candidates = altTexts as IReadOnlyCollection<ApartmentMediaAltText> ?? altTexts.ToList();
            var neutralCulture = GetNeutralCulture(normalizedCulture);
            var neutralDefaultCulture = GetNeutralCulture(DefaultCulture);

            return candidates.FirstOrDefault(t => string.Equals(t.Culture, normalizedCulture, StringComparison.OrdinalIgnoreCase))
                // requested is more specific than stored (e.g. request "en-GB", stored "en")
                ?? candidates.FirstOrDefault(t => string.Equals(t.Culture, neutralCulture, StringComparison.OrdinalIgnoreCase))
                // stored is more specific than requested (e.g. request "de", stored "de-DE"; request "pl", stored "pl-PL")
                ?? candidates.FirstOrDefault(t => string.Equals(GetNeutralCulture(t.Culture), neutralCulture, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(t => string.Equals(t.Culture, DefaultCulture, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(t => string.Equals(GetNeutralCulture(t.Culture), neutralDefaultCulture, StringComparison.OrdinalIgnoreCase))
                ?? candidates.OrderBy(t => t.Id).FirstOrDefault();
        }
    }
}
