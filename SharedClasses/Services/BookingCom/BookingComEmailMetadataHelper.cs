using RentoomBooking.SharedClasses.Models.BookingCom;
using System.Text.RegularExpressions;

namespace RentoomBooking.SharedClasses.Services.BookingCom;

internal static class BookingComEmailMetadataHelper
{
    public const string DefaultProvider = "IDB_PANEL";
    public const string DefaultProviderTransactionId = "IDB_PANEL_TRANSACTION";
    public const string IncomingEmailSource = "incoming_email";
    public const string BackfillSource = "backfill";

    public static int? ExtractReservationId(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        // 1. Najbardziej jednoznaczne przypadki:
        //    "nr 33076", "no 33076", "no. 33076"
        var match = Regex.Match(
            subject,
            @"\b(?:nr|no\.?)\s+(?<id>\d+)\b",
            options);

        // 2. "rezerwacja 33076"
        if (!match.Success)
        {
            match = Regex.Match(
                subject,
                @"\brezerwacj[aię]\s+(?<id>\d+)\b",
                options);
        }

        // 3. "zameldowaniu 33076" / "zameldowaniu  33076"
        if (!match.Success)
        {
            match = Regex.Match(
                subject,
                @"\bzameldowani[ua]\s+(?<id>\d+)\b",
                options);
        }

        // 4. Sam numer przed datą w nawiasie:
        //    "33076 (08.04.2026)"
        if (!match.Success)
        {
            match = Regex.Match(
                subject,
                @"\b(?<id>\d+)\s*(?=\(\d{2}\.\d{2}\.\d{4}\))",
                options);
        }

        return match.Success && int.TryParse(match.Groups["id"].Value, out var reservationId)
            ? reservationId
            : null;
    }

    public static (string Provider, string ProviderTransactionId) ExtractProviderInfo(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return (DefaultProvider, DefaultProviderTransactionId);
        }

        var match = Regex.Match(
            subject,
            @"\((?<content>[^)]+)\)\s*$",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return (DefaultProvider, DefaultProviderTransactionId);
        }

        var bracketContent = match.Groups["content"].Value.Trim();
        if (string.IsNullOrWhiteSpace(bracketContent))
        {
            return (DefaultProvider, DefaultProviderTransactionId);
        }

        var separatorIndex = bracketContent.IndexOf(':');
        var provider = separatorIndex >= 0
            ? bracketContent[..separatorIndex].Trim()
            : bracketContent;

        if (string.IsNullOrWhiteSpace(provider))
        {
            provider = DefaultProvider;
        }

        return (provider, bracketContent);
    }

    public static string BuildSyntheticSubject(int reservationId, string provider, string providerTransactionId)
    {
        var subject = $"Rentoom - Przyjęto rezerwację nr {reservationId}";
        if (string.IsNullOrWhiteSpace(providerTransactionId) ||
            string.Equals(providerTransactionId, DefaultProviderTransactionId, StringComparison.OrdinalIgnoreCase))
        {
            return subject;
        }

        var bracketContent = providerTransactionId.Trim();
        if (!bracketContent.Contains(':') && !string.IsNullOrWhiteSpace(provider))
        {
            bracketContent = $"{provider}: {bracketContent}";
        }

        return $"{subject} ({bracketContent})";
    }

    public static BookingComEmailProcessingContext CreateSyntheticContext(int reservationId, string provider, string providerTransactionId)
    {
        return new BookingComEmailProcessingContext
        {
            ReservationId = reservationId,
            Provider = provider,
            ProviderTransactionId = providerTransactionId,
            IsSynthetic = true,
            Source = BackfillSource
        };
    }
}
